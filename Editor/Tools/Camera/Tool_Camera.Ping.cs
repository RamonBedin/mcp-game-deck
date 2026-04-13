#nullable enable
using System;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Camera
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns a status summary of the camera system: active camera count and whether
        /// the Cinemachine package is installed in the project.
        /// </summary>
        /// <returns>Plain-text summary with camera count and Cinemachine availability.</returns>
        [McpTool("camera-ping", Title = "Camera / Ping", ReadOnlyHint = true)]
        [Description("Health check for the camera system. Reports the number of cameras in the scene " + "and whether Cinemachine is installed. Safe to call at any time — no state is modified.")]
        public ToolResponse PingCamera()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Camera System Status:");
                sb.AppendLine($"  Cameras in scene: {Camera.allCameras.Length}");
                sb.AppendLine($"  Main Camera: {(Camera.main != null ? Camera.main.gameObject.name : "none")}");
                sb.AppendLine($"  Cinemachine installed: {IsCinemachineInstalled()}");

                if (IsCinemachineInstalled())
                {
                    Type? cmType = GetCinemachineCameraType();

                    if (cmType != null)
                    {
                        sb.AppendLine($"  Cinemachine type: {cmType.FullName}");
                    }

                    UnityEngine.Component? brain = FindCinemachineBrain();
                    sb.AppendLine($"  CinemachineBrain: {(brain != null ? brain.gameObject.name : "not found")}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region CINEMACHINE REFLECTION HELPERS

        /// <summary>
        /// Returns true when at least one known Cinemachine assembly is loaded in the current domain.
        /// Supports both the modern Unity.Cinemachine (com.unity.cinemachine 3.x) and the legacy
        /// Cinemachine (com.unity.cinemachine 2.x) assemblies.
        /// Exposed as <c>internal</c> so <see cref="Helpers.CinemachineHelper"/> can call it without duplication.
        /// </summary>
        /// <returns>True if Cinemachine is available; otherwise false.</returns>
        internal static bool IsCinemachineInstalled()
        {
            return Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine") != null || Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine") != null;
        }

        /// <summary>
        /// Returns the runtime Type of CinemachineCamera (v3) or CinemachineVirtualCamera (v2),
        /// whichever is present, or null if Cinemachine is not installed.
        /// </summary>
        /// <returns>The Cinemachine virtual camera type, or null.</returns>
        private static Type? GetCinemachineCameraType()
        {
            Type? t = Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine");

            if (t != null)
            {
                return t;
            }

            return Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");
        }

        /// <summary>
        /// Returns the runtime Type of CinemachineBrain (v3) or CinemachineBrain (v2),
        /// whichever is present, or null if Cinemachine is not installed.
        /// </summary>
        /// <returns>The CinemachineBrain type, or null.</returns>
        private static Type? GetCinemachineBrainType()
        {
            Type? t = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");

            if (t != null)
            {
                return t;
            }

            return Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
        }

        /// <summary>
        /// Finds a Cinemachine virtual camera component on a GameObject using reflection.
        /// Works for both v2 and v3 assemblies.
        /// Exposed as <c>internal</c> so <see cref="Helpers.CinemachineHelper"/> can call it without duplication.
        /// </summary>
        /// <param name="go">The GameObject to search.</param>
        /// <returns>The Cinemachine camera Component, or null when not found.</returns>
        internal static UnityEngine.Component? FindCinemachineCamera(GameObject go)
        {
            Type? camType = GetCinemachineCameraType();

            if (camType == null)
            {
                return null;
            }

            return go.GetComponent(camType);
        }

        /// <summary>
        /// Finds a CinemachineBrain component anywhere in the loaded scenes using reflection.
        /// </summary>
        /// <returns>The first CinemachineBrain Component found, or null.</returns>
        private static UnityEngine.Component? FindCinemachineBrain()
        {
            Type? brainType = GetCinemachineBrainType();

            if (brainType == null)
            {
                return null;
            }

            UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(brainType, FindObjectsSortMode.None);

            if (found.Length == 0)
            {
                return null;
            }

            return found[0] as UnityEngine.Component;
        }

        /// <summary>
        /// Reads a named property value from a component via reflection.
        /// </summary>
        /// <param name="target">The component to read from.</param>
        /// <param name="propertyName">The property name.</param>
        /// <returns>The property value, or null when not found.</returns>
        private static object? GetProperty(object target, string propertyName)
        {
            var prop = target.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return prop?.GetValue(target);
        }

        /// <summary>
        /// Sets a named property value on a component via reflection.
        /// </summary>
        /// <param name="target">The component to write to.</param>
        /// <param name="propertyName">The property name.</param>
        /// <param name="value">The new value.</param>
        /// <returns>True if the property was found and set; otherwise false.</returns>
        private static bool SetProperty(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (prop == null || !prop.CanWrite)
            {
                return false;
            }

            prop.SetValue(target, value);
            return true;
        }

        /// <summary>
        /// Reads a named field value from a component via reflection.
        /// </summary>
        /// <param name="target">The object to read from.</param>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The field value, or null when not found.</returns>
        private static object? GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(target);
        }

        /// <summary>
        /// Sets a named field value on a component via reflection.
        /// </summary>
        /// <param name="target">The object to write to.</param>
        /// <param name="fieldName">The field name.</param>
        /// <param name="value">The new value.</param>
        /// <returns>True if the field was found and set; otherwise false.</returns>
        private static bool SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (field == null)
            {
                return false;
            }

            field.SetValue(target, value);
            return true;
        }

        #endregion
    }
}