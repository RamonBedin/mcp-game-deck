#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating, assigning, updating, and inspecting Material assets in the project.
    /// Covers material creation with auto-detected shaders, renderer assignment, property updates,
    /// and detailed shader property queries.
    /// </summary>
    [McpToolType]
    public partial class Tool_Material
    {
        #region Assign Tool

        /// <summary>
        /// Assigns a Material asset to a GameObject's Renderer component.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. 'Canvas/Panel'). Used when instanceId is 0.</param>
        /// <param name="materialPath">Asset path of the material to assign (e.g. 'Assets/Materials/Red.mat').</param>
        /// <param name="materialIndex">Index of the material slot on the Renderer. Default 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the material name, target GameObject, and slot index,
        /// or an error when the GameObject, material, or Renderer is not found, or the index is out of range.
        /// </returns>
        [McpTool("material-assign", Title = "Material / Assign")]
        [Description("Assigns a Material asset to a GameObject's Renderer at the specified material index.")]
        public ToolResponse Assign(
            [Description("Instance ID of the target GameObject. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. '/Canvas/Panel').")] string objectPath = "",
            [Description("Asset path of the material (e.g. 'Assets/Materials/Red.mat').")] string materialPath = "",
            [Description("Index of the material slot on the Renderer. Default 0.")] int materialIndex = 0
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                if (string.IsNullOrWhiteSpace(materialPath))
                {
                    return ToolResponse.Error("materialPath is required.");
                }

                if (!materialPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("materialPath must start with 'Assets/'.");
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                if (material == null)
                {
                    return ToolResponse.Error($"Material not found at '{materialPath}'.");
                }

                if (!go.TryGetComponent<Renderer>(out var renderer))
                {
                    return ToolResponse.Error($"'{go.name}' has no Renderer component.");
                }

                var materials = renderer.sharedMaterials;

                if (materialIndex < 0 || materialIndex >= materials.Length)
                {
                    return ToolResponse.Error($"Material index {materialIndex} out of range (0-{materials.Length - 1}).");
                }

                Undo.RecordObject(renderer, "Assign Material");
                materials[materialIndex] = material;
                renderer.sharedMaterials = materials;

                return ToolResponse.Text($"Assigned '{material.name}' to '{go.name}' at slot {materialIndex}.");
            });
        }

        #endregion
    }
}