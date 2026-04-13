#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using GameDeck.Editor.Tools.Helpers;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating, inspecting, and managing UI Toolkit assets (UXML, USS,
    /// PanelSettings), attaching UIDocuments to GameObjects, reading/writing UI files,
    /// and dumping live visual element trees at runtime.
    /// </summary>
    [McpToolType]
    public partial class Tool_UIToolkit
    {
        #region TOOL METHODS

        /// <summary>
        /// Finds a GameObject in the scene, adds a <see cref="UIDocument"/> component to it, assigns
        /// the source UXML asset, and optionally assigns a PanelSettings asset.
        /// </summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Takes priority over objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Canvas/HUD"). Used when instanceId is 0.</param>
        /// <param name="uxmlPath">Asset path of the UXML file to assign (e.g. "Assets/UI/HUD.uxml").</param>
        /// <param name="panelSettingsPath">Asset path of the PanelSettings asset. Leave empty to skip assignment.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the UIDocument was attached, or an error message.</returns>
        [McpTool("uitoolkit-attach-document", Title = "UI Toolkit / Attach Document")]
        [Description("Adds a UIDocument component to a GameObject and assigns a UXML source asset. " + "Optionally assigns PanelSettings. Finds the GameObject by instanceId or hierarchy path.")]
        public ToolResponse AttachDocument(
            [Description("Instance ID of the target GameObject. Use 0 to find by objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Canvas/HUD'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Asset path of the UXML file to assign (e.g. 'Assets/UI/HUD.uxml').")] string uxmlPath = "",
            [Description("Asset path of the PanelSettings asset. Leave empty to skip.")] string panelSettingsPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(uxmlPath))
                {
                    return ToolResponse.Error("uxmlPath is required.");
                }

                if (!uxmlPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("uxmlPath must start with 'Assets/' (e.g. 'Assets/UI/HUD.uxml').");
                }

                if (!string.IsNullOrWhiteSpace(panelSettingsPath) && !panelSettingsPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("panelSettingsPath must start with 'Assets/' (e.g. 'Assets/UI/GamePanelSettings.asset').");
                }

                if (!UIToolkitHelper.TryResolveGameObject(instanceId, objectPath, out var go, out var goError))
                {
                    return ToolResponse.Error(goError!);
                }

                var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

                if (uxmlAsset == null)
                {
                    return ToolResponse.Error($"UXML asset not found at '{uxmlPath}'.");
                }

                var doc = go.GetComponent<UIDocument>();
                bool wasAdded = false;

                if (doc == null)
                {
                    Undo.RecordObject(go, "Attach UIDocument");
                    doc = Undo.AddComponent<UIDocument>(go);
                    wasAdded = true;
                }
                else
                {
                    Undo.RecordObject(doc, "Attach UIDocument");
                }

                doc.visualTreeAsset = uxmlAsset;

                if (!string.IsNullOrWhiteSpace(panelSettingsPath))
                {
                    var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);

                    if (panelSettings == null)
                    {
                        return ToolResponse.Error($"PanelSettings asset not found at '{panelSettingsPath}'.");
                    }

                    doc.panelSettings = panelSettings;
                }

                EditorUtility.SetDirty(go);

                string action = wasAdded ? "Added UIDocument to" : "Updated UIDocument on";
                return ToolResponse.Text($"{action} '{go.name}'. UXML: '{uxmlPath}'" + (string.IsNullOrWhiteSpace(panelSettingsPath) ? "." : $", PanelSettings: '{panelSettingsPath}'."));
            });
        }

        #endregion
    }
}