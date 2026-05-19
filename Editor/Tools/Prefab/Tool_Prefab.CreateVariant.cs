#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a Prefab Variant asset from a scene prefab instance. The target GameObject
        /// must already be a prefab instance root (it has a source prefab); Unity then
        /// automatically marks the new asset as a Variant of that source.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the scene prefab instance root. Pass 0 to use objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the scene prefab instance root. Used when instanceId is 0.</param>
        /// <param name="savePath">Asset path to save the new variant (e.g. 'Assets/Prefabs/Enemy_Boss.prefab').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the variant name and saved path, or an error.</returns>
        [McpTool("prefab-create-variant", Title = "Prefab / Create Variant")]
        [Description("Creates a Prefab Variant asset from a scene prefab instance. The target GameObject must already be a prefab instance root; Unity auto-marks the new asset as a Variant of the source prefab. To create a regular (non-variant) prefab from a scene GameObject, use 'prefab-create' instead.")]
        public ToolResponse CreateVariant(
            [Description("Instance ID of the scene prefab instance root. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the scene prefab instance root (e.g. 'World/Enemies/BossInstance'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Asset path to save the new variant (e.g. 'Assets/Prefabs/Enemy_Boss.prefab').")] string savePath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    return ToolResponse.Error($"'{go.name}' is not a prefab instance root. Variants can only be created from existing prefab instances.");
                }

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    return ToolResponse.Error("savePath is required (e.g. 'Assets/Prefabs/Enemy_Variant.prefab').");
                }

                if (!savePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("savePath must start with 'Assets/' (e.g. 'Assets/Prefabs/Enemy_Variant.prefab').");
                }

                string folder = System.IO.Path.GetDirectoryName(savePath) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);
                GameObject? variant = PrefabUtility.SaveAsPrefabAsset(go, savePath, out bool success);

                if (!success || variant == null)
                {
                    return ToolResponse.Error($"Failed to create prefab variant at '{savePath}'.");
                }

                return ToolResponse.Text($"Created prefab variant '{variant.name}' at {savePath}.");
            });
        }

        #endregion
    }
}