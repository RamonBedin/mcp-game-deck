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
        #region TOOL METHODS

        /// <summary>
        /// Applies lens settings to a camera. Works on regular Unity cameras and Cinemachine
        /// virtual cameras. Pass -1 for any float parameter to leave that value unchanged.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject to modify.</param>
        /// <param name="fov">Vertical field of view in degrees. Pass -1 to skip.</param>
        /// <param name="nearClip">Near clipping plane distance. Pass -1 to skip.</param>
        /// <param name="farClip">Far clipping plane distance. Pass -1 to skip.</param>
        /// <param name="orthoSize">Orthographic half-height. Pass -1 to skip.</param>
        /// <returns>Confirmation text listing each lens setting that was changed.</returns>
        [McpTool("camera-set-lens", Title = "Camera / Set Lens")]
        [Description("Configures lens settings on a camera (regular or Cinemachine). " + "Adjusts field of view, clipping planes, and orthographic size. " + "Pass -1 for any value to leave it unchanged.")]
        public ToolResponse SetLens(
            [Description("Name of the camera GameObject to modify.")] string cameraName,
            [Description("Vertical field of view in degrees. -1 to skip.")] float fov = -1f,
            [Description("Near clipping plane distance. -1 to skip.")] float nearClip = -1f,
            [Description("Far clipping plane distance. -1 to skip.")] float farClip = -1f,
            [Description("Orthographic half-height. -1 to skip.")] float orthoSize = -1f)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(cameraName))
                {
                    return ToolResponse.Error("cameraName is required.");
                }

                var go = GameObject.Find(cameraName);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject '{cameraName}' not found.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Set lens on '{cameraName}':");

                if (go.TryGetComponent<Camera>(out var cam))
                {
                    Undo.RecordObject(cam, $"Set Lens {cameraName}");

                    if (fov > 0f)
                    {
                        cam.fieldOfView = fov;
                        sb.AppendLine($"  FOV: {fov}");
                    }

                    if (nearClip > 0f)
                    {
                        cam.nearClipPlane = nearClip;
                        sb.AppendLine($"  Near: {nearClip}");
                    }

                    if (farClip > 0f)
                    {
                        cam.farClipPlane = farClip;
                        sb.AppendLine($"  Far: {farClip}");
                    }

                    if (orthoSize > 0f)
                    {
                        cam.orthographicSize = orthoSize;
                        sb.AppendLine($"  OrthoSize: {orthoSize}");
                    }

                    EditorUtility.SetDirty(cam);
                }

                if (IsCinemachineInstalled())
                {
                    UnityEngine.Component? cmCam = FindCinemachineCamera(go);

                    if (cmCam != null)
                    {
                        Undo.RecordObject(cmCam, $"Set CM Lens {cameraName}");

                        object? lensObj = GetProperty(cmCam, "Lens");
                        lensObj ??= GetField(cmCam, "m_Lens");

                        if (lensObj != null)
                        {
                            Type lensType = lensObj.GetType();

                            if (fov > 0f)
                            {
                                var fovProp = lensType.GetProperty("FieldOfView", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (fovProp != null && fovProp.CanWrite)
                                {
                                    fovProp.SetValue(lensObj, fov);
                                    sb.AppendLine($"  CM FOV: {fov}");
                                }
                            }

                            if (nearClip > 0f)
                            {
                                var prop = lensType.GetProperty("NearClipPlane", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (prop != null && prop.CanWrite)
                                {
                                    prop.SetValue(lensObj, nearClip);
                                    sb.AppendLine($"  CM Near: {nearClip}");
                                }
                            }

                            if (farClip > 0f)
                            {
                                var prop = lensType.GetProperty("FarClipPlane", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (prop != null && prop.CanWrite)
                                {
                                    prop.SetValue(lensObj, farClip);
                                    sb.AppendLine($"  CM Far: {farClip}");
                                }
                            }

                            if (orthoSize > 0f)
                            {
                                var prop = lensType.GetProperty("OrthographicSize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                                if (prop != null && prop.CanWrite)
                                {
                                    prop.SetValue(lensObj, orthoSize);
                                    sb.AppendLine($"  CM OrthoSize: {orthoSize}");
                                }
                            }

                            bool wrote = SetProperty(cmCam, "Lens", lensObj);

                            if (!wrote)
                            {
                                SetField(cmCam, "m_Lens", lensObj);
                            }
                        }

                        EditorUtility.SetDirty(cmCam);
                    }
                }

                if (cam == null && !IsCinemachineInstalled())
                {
                    return ToolResponse.Error($"'{cameraName}' has no Camera or Cinemachine component.");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}