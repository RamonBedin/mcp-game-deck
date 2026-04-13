#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UIToolkit
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new <see cref="PanelSettings"/> asset at the given path and configures
        /// its scale mode and reference resolution.
        /// </summary>
        /// <param name="path">Asset path for the new PanelSettings (e.g. "Assets/UI/GamePanelSettings.asset").</param>
        /// <param name="scaleMode">Scale mode: "ConstantPixelSize", "ScaleWithScreenSize", or "ConstantPhysicalSize". Defaults to "ConstantPixelSize".</param>
        /// <param name="referenceWidth">Reference screen width in pixels. Used with ScaleWithScreenSize. Defaults to 1920.</param>
        /// <param name="referenceHeight">Reference screen height in pixels. Used with ScaleWithScreenSize. Defaults to 1080.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming creation, or an error message.</returns>
        [McpTool("uitoolkit-create-panel-settings", Title = "UI Toolkit / Create Panel Settings")]
        [Description("Creates a PanelSettings asset at the specified path and configures scale mode and reference resolution.")]
        public ToolResponse CreatePanelSettings(
            [Description("Asset path for the new PanelSettings asset (e.g. 'Assets/UI/GamePanelSettings.asset').")] string path,
            [Description("Scale mode: 'ConstantPixelSize', 'ScaleWithScreenSize', or 'ConstantPhysicalSize'. Defaults to 'ConstantPixelSize'.")] string scaleMode = "ConstantPixelSize",
            [Description("Reference screen width in pixels, used with ScaleWithScreenSize. Defaults to 1920.")] int referenceWidth = 1920,
            [Description("Reference screen height in pixels, used with ScaleWithScreenSize. Defaults to 1080.")] int referenceHeight = 1080
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                if (!path.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("path must start with 'Assets/' (e.g. 'Assets/UI/GamePanelSettings.asset').");
                }

                if (!path.EndsWith(".asset"))
                {
                    return ToolResponse.Error("path must end with .asset.");
                }

                var dir = Path.GetDirectoryName(path)!;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = UnityEngine.ScriptableObject.CreateInstance<PanelSettings>();

                switch (scaleMode)
                {
                    case "ScaleWithScreenSize":
                        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                        settings.referenceResolution = new UnityEngine.Vector2Int(referenceWidth, referenceHeight);
                        break;

                    case "ConstantPhysicalSize":
                        settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
                        break;

                    default:
                        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                        break;
                }

                AssetDatabase.CreateAsset(settings, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path);

                return ToolResponse.Text($"Created PanelSettings at '{path}'. ScaleMode: {settings.scaleMode}" + (settings.scaleMode == PanelScaleMode.ScaleWithScreenSize ? $", Reference: {referenceWidth}x{referenceHeight}." : "."));
            });
        }

        #endregion
    }
}