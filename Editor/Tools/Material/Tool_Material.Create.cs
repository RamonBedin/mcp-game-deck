#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Material
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new Material asset with the specified shader and saves it to disk.
        /// </summary>
        /// <param name="name">Name for the new material asset.</param>
        /// <param name="shaderName">
        /// Shader to use (e.g. "Standard", "Universal Render Pipeline/Lit").
        /// Leave empty to auto-detect based on the active render pipeline.
        /// </param>
        /// <param name="savePath">Project folder path to save the asset (e.g. "Assets/Materials"). Default "Assets".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the material name, asset path, and shader used,
        /// or an error when the name is missing, the save path is invalid, or no valid shader is found.
        /// </returns>
        [McpTool("material-create", Title = "Material / Create")]
        [Description("Creates a new Material asset with the specified shader and saves it to the project.")]
        public ToolResponse Create(
            [Description("Name for the new material.")] string name,
            [Description("Shader name (e.g. 'Standard', 'Universal Render Pipeline/Lit'). Leave empty for auto-detect.")] string shaderName = "",
            [Description("Folder path to save the material (e.g. 'Assets/Materials'). Default 'Assets'.")] string savePath = "Assets"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return ToolResponse.Error("name is required.");
                }

                if (!savePath.StartsWith("Assets/") && savePath != "Assets")
                {
                    return ToolResponse.Error("savePath must start with 'Assets/'.");
                }

                Shader? shader = null;

                if (!string.IsNullOrWhiteSpace(shaderName))
                {
                    shader = Shader.Find(shaderName);
                }

                if (shader == null)
                {
                    var rp = GraphicsSettings.currentRenderPipeline;

                    if (rp != null)
                    {
                        string rpName = rp.GetType().Name;

                        if (rpName.Contains("Universal") || rpName.Contains("URP"))
                        {
                            shader = Shader.Find("Universal Render Pipeline/Lit");
                        }
                        else if (rpName.Contains("HD") || rpName.Contains("HDRP"))
                        {
                            shader = Shader.Find("HDRP/Lit");
                        }
                    }
                }

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    return ToolResponse.Error("Could not find a valid shader.");
                }

                var material = new Material(shader)
                {
                    name = name
                };

                if (!AssetDatabase.IsValidFolder(savePath))
                {
                    System.IO.Directory.CreateDirectory(savePath);
                    AssetDatabase.Refresh();
                }

                string assetPath = $"{savePath}/{name}.mat";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                AssetDatabase.CreateAsset(material, assetPath);
                AssetDatabase.SaveAssets();

                return ToolResponse.Text($"Created material '{name}' at {assetPath} with shader '{shader.name}'.");
            });
        }

        #endregion
    }
}