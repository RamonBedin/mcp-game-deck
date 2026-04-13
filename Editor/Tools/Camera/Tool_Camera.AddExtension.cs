#nullable enable
using System;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Camera
    {
        #region ADD EXTENSION

        /// <summary>
        /// Resolves the Cinemachine extension type identified by <paramref name="extensionType"/>
        /// and attaches it to the camera GameObject found by <paramref name="cameraName"/> or
        /// <paramref name="instanceId"/>. The operation is recorded on the Undo stack.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject. Used when <paramref name="instanceId"/> is 0.</param>
        /// <param name="instanceId">Unity instance ID of the camera GameObject. Takes priority over <paramref name="cameraName"/>.</param>
        /// <param name="extensionType">
        /// Simple or fully-qualified name of the Cinemachine extension to add
        /// (e.g. 'CinemachineConfiner2D', 'CinemachineCollider').
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the component was added,
        /// or an error when Cinemachine is not installed or the type is not found.
        /// </returns>
        [McpTool("camera-add-extension", Title = "Camera / Add Extension")]
        [Description("Adds a Cinemachine extension component to a virtual camera. " + "extensionType is the simple or fully-qualified component name " + "(e.g. 'CinemachineConfiner2D', 'CinemachineCollider'). " + "Requires Cinemachine to be installed.")]
        public ToolResponse AddExtension(
            [Description("Name of the camera GameObject. Used when instanceId is 0.")] string cameraName = "",
            [Description("Unity instance ID of the camera GameObject. Takes priority over cameraName.")] int instanceId = 0,
            [Description("Simple or fully-qualified Cinemachine extension type name (e.g. 'CinemachineConfiner2D').")] string extensionType = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(extensionType))
                {
                    return ToolResponse.Error("extensionType is required.");
                }

                if (!IsCinemachineInstalled())
                {
                    return ToolResponse.Error("Cinemachine is not installed in this project.");
                }

                GameObject? go = FindCameraGameObject(instanceId, cameraName);

                if (go == null)
                {
                    return ToolResponse.Error($"Camera GameObject not found. instanceId={instanceId}, cameraName='{cameraName}'.");
                }

                Type? extType = ResolveCinemachineType(extensionType);

                if (extType == null)
                {
                    return ToolResponse.Error($"Cinemachine extension type '{extensionType}' not found. " + "Verify the type name and that the required Cinemachine package is installed.");
                }

                if (go.GetComponent(extType) != null)
                {
                    return ToolResponse.Text($"'{extType.Name}' is already present on '{go.name}' — no change made.");
                }

                UnityEngine.Component added = Undo.AddComponent(go, extType);

                if (added == null)
                {
                    return ToolResponse.Error($"Failed to add '{extType.Name}' to '{go.name}'.");
                }

                EditorUtility.SetDirty(go);

                var sb = new StringBuilder();
                sb.AppendLine($"Added Cinemachine extension to '{go.name}':");
                sb.AppendLine($"  Type: {extType.FullName}");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE CAMERA / CINEMACHINE HELPERS

        /// <summary>
        /// Finds a camera GameObject by instance ID or name.
        /// </summary>
        /// <param name="instanceId">Unity instance ID. Non-zero takes priority.</param>
        /// <param name="name">GameObject name to search for when instanceId is 0.</param>
        /// <returns>The matching <see cref="GameObject"/>, or null.</returns>
        private static GameObject? FindCameraGameObject(int instanceId, string name)
        {
            if (instanceId != 0)
            {
                var obj = EditorUtility.EntityIdToObject(instanceId) as GameObject;

                if (obj != null)
                {
                    return obj;
                }
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return GameObject.Find(name);
            }

            return null;
        }

        /// <summary>
        /// Searches Cinemachine assemblies (v3 Unity.Cinemachine and v2 Cinemachine) for a
        /// type matching <paramref name="typeName"/> by simple or fully-qualified name.
        /// Falls back to a domain-wide search when not found in known assemblies.
        /// </summary>
        /// <param name="typeName">Simple or fully-qualified type name.</param>
        /// <returns>The resolved <see cref="Type"/>, or null when not found.</returns>
        private static Type? ResolveCinemachineType(string typeName)
        {
            string[] assemblyNames = { "Unity.Cinemachine", "Cinemachine" };

            for (int a = 0; a < assemblyNames.Length; a++)
            {
                string qualified = $"{typeName}, {assemblyNames[a]}";
                Type? t = Type.GetType(qualified);

                if (t != null)
                {
                    return t;
                }

                string ns = assemblyNames[a] == "Unity.Cinemachine" ? "Unity.Cinemachine" : "Cinemachine";
                qualified = $"{ns}.{typeName}, {assemblyNames[a]}";
                t = Type.GetType(qualified);

                if (t != null)
                {
                    return t;
                }
            }

            System.Reflection.Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < loaded.Length; i++)
            {
                Type[] types;
                try
                {
                    types = loaded[i].GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    continue;
                }

                for (int j = 0; j < types.Length; j++)
                {
                    if (types[j] != null && string.Equals(types[j].Name, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return types[j];
                    }
                }
            }

            return null;
        }

        #endregion
    }
}