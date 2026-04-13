#nullable enable
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.Editor.Tools.Helpers
{
    /// <summary>
    /// Shared helper methods for Cinemachine tool implementations.
    /// Eliminates repeated Cinemachine camera-lookup boilerplate across tool files.
    /// </summary>
    internal static class CinemachineHelper
    {
        #region CINEMACHINE CAMERA LOOKUP

        /// <summary>
        /// Finds a Cinemachine virtual camera component by locating the named GameObject and
        /// retrieving its Cinemachine camera component via <see cref="Tool_Camera.FindCinemachineCamera"/>.
        /// Validates that Cinemachine is installed, that the GameObject exists, and that it carries
        /// a Cinemachine camera component.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject. Must not be null or whitespace.</param>
        /// <param name="cmCam">
        /// The resolved Cinemachine camera <see cref="Component"/>, or <c>null</c> if not found.
        /// </param>
        /// <param name="error">
        /// A pre-built <see cref="ToolResponse"/> error when lookup fails;
        /// <c>null</c> when <paramref name="cmCam"/> is valid.
        /// </param>
        /// <returns><c>true</c> when a valid Cinemachine camera was found; <c>false</c> otherwise.</returns>
        public static bool TryGetCinemachineCamera(string cameraName, out Component? cmCam, out ToolResponse? error)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
            {
                cmCam = null;
                error = ToolResponse.Error("cameraName is required.");
                return false;
            }

            if (!Tool_Camera.IsCinemachineInstalled())
            {
                cmCam = null;
                error = ToolResponse.Error("Cinemachine is not installed in this project.");
                return false;
            }

            var go = GameObject.Find(cameraName);

            if (go == null)
            {
                cmCam = null;
                error = ToolResponse.Error($"GameObject '{cameraName}' not found.");
                return false;
            }

            cmCam = Tool_Camera.FindCinemachineCamera(go);

            if (cmCam == null)
            {
                error = ToolResponse.Error($"'{cameraName}' has no Cinemachine camera component.");
                return false;
            }

            error = null;
            return true;
        }

        #endregion
    }
}