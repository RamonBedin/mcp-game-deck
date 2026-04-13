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
    /// MCP tools for creating, configuring, and inspecting Unity Terrain objects.
    /// Covers terrain creation with TerrainData assets, heightmap and layer configuration,
    /// and detailed terrain property queries.
    /// </summary>
    [McpToolType]
    public partial class Tool_Terrain
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new Terrain GameObject with a TerrainData asset.
        /// <param name="name">Name for the Terrain GameObject. Default "Terrain".</param>
        /// <param name="width">Terrain width in world units. Default 500.</param>
        /// <param name="height">Maximum terrain height in world units. Default 200.</param>
        /// <param name="length">Terrain length (depth) in world units. Default 500.</param>
        /// <param name="heightmapResolution">Heightmap resolution — must be a power of 2 plus 1 (e.g. 513). Default 513.</param>
        /// <param name="savePath">Project folder path to save the TerrainData asset. Default "Assets".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the terrain name, dimensions, and data asset path,
        /// or an error if the save path is invalid.
        /// </returns>
        /// </summary>
        [McpTool("terrain-create", Title = "Terrain / Create")]
        [Description("Creates a new Terrain object with the specified dimensions and resolution.")]
        public ToolResponse Create(
            [Description("Name for the terrain. Default 'Terrain'.")] string name = "Terrain",
            [Description("Terrain width in world units. Default 500.")] float width = 500f,
            [Description("Terrain height in world units. Default 200.")] float height = 200f,
            [Description("Terrain length (depth) in world units. Default 500.")] float length = 500f,
            [Description("Heightmap resolution (power of 2 + 1). Default 513.")] int heightmapResolution = 513,
            [Description("Save path for terrain data. Default 'Assets'.")] string savePath = "Assets"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!savePath.StartsWith("Assets/") && savePath != "Assets")
                {
                    return ToolResponse.Error("savePath must start with 'Assets/'.");
                }

                var terrainData = new TerrainData
                {
                    heightmapResolution = heightmapResolution,
                    size = new Vector3(width, height, length)
                };

                string assetPath = $"{savePath}/{name}_Data.asset";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                AssetDatabase.CreateAsset(terrainData, assetPath);

                var go = Terrain.CreateTerrainGameObject(terrainData);
                go.name = name;

                Undo.RegisterCreatedObjectUndo(go, $"Create Terrain {name}");

                return ToolResponse.Text($"Created terrain '{name}' ({width}x{length}, height {height}). Data at {assetPath}.");
            });
        }

        #endregion
    }
}