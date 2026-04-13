#nullable enable
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.Editor.Tools.Helpers
{
    /// <summary>
    /// Shared helper methods for Camera tool implementations.
    /// Eliminates repeated camera-lookup boilerplate across tool files.
    /// </summary>
    internal static class CameraHelper
    {
        #region CAMERA LOOKUP

        /// <summary>
        /// Finds a camera by name, or returns <see cref="Camera.main"/> when
        /// <paramref name="cameraName"/> is <c>null</c> or empty.
        /// </summary>
        /// <param name="cameraName">
        /// Optional camera name. When <c>null</c> or whitespace, <see cref="Camera.main"/> is used.
        /// </param>
        /// <param name="camera">The resolved <see cref="Camera"/>, or <c>null</c> if not found.</param>
        /// <param name="error">
        /// A pre-built <see cref="ToolResponse"/> error when the camera is not found;
        /// <c>null</c> when <paramref name="camera"/> is valid.
        /// </param>
        /// <returns><c>true</c> when a valid camera was found; <c>false</c> otherwise.</returns>
        public static bool TryGet(string? cameraName, out Camera? camera, out ToolResponse? error)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
            {
                camera = Camera.main;

                if (camera == null)
                {
                    error = ToolResponse.Error("No main camera found in the current scene.");
                    return false;
                }

                error = null;
                return true;
            }

            var go = GameObject.Find(cameraName);

            if (go == null)
            {
                camera = null;
                error = ToolResponse.Error($"GameObject '{cameraName}' not found.");
                return false;
            }

            camera = go.GetComponent<Camera>();

            if (camera == null)
            {
                error = ToolResponse.Error($"GameObject '{cameraName}' does not have a Camera component.");
                return false;
            }

            error = null;
            return true;
        }

        #endregion
    }
}