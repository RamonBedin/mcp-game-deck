#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Terrain
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns information about a Terrain in the scene.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the Terrain's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Name or hierarchy path of the Terrain's GameObject. Leave empty to use the first active Terrain.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with position, size, heightmap resolution, detail resolution,
        /// alpha map resolution, terrain layer count, and tree instance count,
        /// or an error if no Terrain is found.
        /// </returns>
        [McpTool("terrain-get-info", Title = "Terrain / Get Info", ReadOnlyHint = true)]
        [Description("Returns information about the active Terrain or a Terrain found by name.")]
        public ToolResponse GetInfo(
            [Description("Instance ID of the Terrain GameObject. 0 to use objectPath.")] int instanceId = 0,
            [Description("Name or path of the Terrain GameObject. Empty uses the first Terrain found.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Terrain? terrain = null;

                if (instanceId != 0 || !string.IsNullOrWhiteSpace(objectPath))
                {
                    var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                    if (go != null)
                    {
                        terrain = go.GetComponent<Terrain>();
                    }
                }

                if (terrain == null)
                {
                    terrain = Terrain.activeTerrain;
                }

                if (terrain == null)
                {
                    return ToolResponse.Error("No Terrain found in scene.");
                }

                var data = terrain.terrainData;
                var sb = new StringBuilder();
                sb.AppendLine($"Terrain: {terrain.gameObject.name}");
                sb.AppendLine($"  Position: {terrain.transform.position}");
                sb.AppendLine($"  Size: {data.size}");
                sb.AppendLine($"  Heightmap Resolution: {data.heightmapResolution}");
                sb.AppendLine($"  Detail Resolution: {data.detailResolution}");
                sb.AppendLine($"  Alpha Map Resolution: {data.alphamapResolution}");
                sb.AppendLine($"  Terrain Layers: {data.terrainLayers.Length}");
                sb.AppendLine($"  Tree Instance Count: {data.treeInstanceCount}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}