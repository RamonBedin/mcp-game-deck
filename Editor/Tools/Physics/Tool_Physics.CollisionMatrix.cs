#nullable enable
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Physics
    {
        #region TOOL METHODS

        /// <summary>
        /// Reads the current physics collision matrix and reports which named layer pairs
        /// are set to ignore each other.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> listing all ignored layer pairs and the total named-layer inventory.</returns>
        [McpTool("physics-get-collision-matrix", Title = "Physics / Get Collision Matrix")]
        [Description("Gets the physics collision matrix showing which layers collide with each other. " + "Only displays layers that have names assigned. Shows ignored (non-colliding) layer pairs.")]
        public ToolResponse GetCollisionMatrix()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var layers = new List<(int index, string name)>();

                for (int i = 0; i < 32; i++)
                {
                    var name = LayerMask.LayerToName(i);

                    if (!string.IsNullOrEmpty(name))
                    {
                        layers.Add((i, name));
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine("Physics Collision Matrix (ignored pairs):");
                sb.AppendLine();
                bool anyIgnored = false;

                for (int a = 0; a < layers.Count; a++)
                {
                    for (int b = a; b < layers.Count; b++)
                    {
                        bool ignored = Physics.GetIgnoreLayerCollision(layers[a].index, layers[b].index);

                        if (ignored)
                        {
                            sb.AppendLine($"  {layers[a].name} <-> {layers[b].name} : IGNORED (no collision)");
                            anyIgnored = true;
                        }
                    }
                }

                if (!anyIgnored)
                {
                    sb.AppendLine("  All named layers collide with each other (no ignored pairs).");
                }

                sb.AppendLine();
                sb.AppendLine($"Total named layers: {layers.Count}");

                foreach (var (index, name) in layers)
                {
                    sb.AppendLine($"  [{index}] {name}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>
        /// Sets whether two physics layers collide with each other by updating the collision matrix.
        /// </summary>
        /// <param name="layerA">First layer — accepts a layer name or numeric index string.</param>
        /// <param name="layerB">Second layer — accepts a layer name or numeric index string.</param>
        /// <param name="collide">True means the layers collide; false means they ignore each other.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the updated collision state, or an error if a layer is not found.</returns>
        [McpTool
        (
            "physics-set-collision-matrix",
            Title = "Physics / Set Collision Matrix"
        )]
        [Description("Sets whether two physics layers collide with each other. " +
            "Accepts layer names or numeric indices.")]
        public ToolResponse SetCollisionMatrix(
            [Description("First layer name or numeric index (e.g. 'Default' or '0').")] string layerA,
            [Description("Second layer name or numeric index (e.g. 'Water' or '4').")] string layerB,
            [Description("Whether the two layers should collide (true = collide, false = ignore).")] bool collide
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                int indexA = ResolveLayerIndex(layerA);

                if (indexA < 0)
                {
                    return ToolResponse.Error($"Layer not found: '{layerA}'");
                }

                int indexB = ResolveLayerIndex(layerB);

                if (indexB < 0)
                {
                    return ToolResponse.Error($"Layer not found: '{layerB}'");
                }

                Physics.IgnoreLayerCollision(indexA, indexB, !collide);
                var nameA = LayerMask.LayerToName(indexA);
                var nameB = LayerMask.LayerToName(indexB);

                var state = collide ? "collide" : "ignore";
                return ToolResponse.Text($"Set layers '{nameA}' [{indexA}] and '{nameB}' [{indexB}] to {state}.");
            });
        }

        #endregion

        #region HELPER METHODS

        /// <summary>
        /// Resolves a layer identifier — either a numeric string or a named layer string —
        /// to its 0-based layer index. Returns -1 when no match is found.
        /// </summary>
        /// <param name="layer">Layer name or numeric index as a string.</param>
        /// <returns>The resolved layer index, or -1 if the layer does not exist.</returns>
        private static int ResolveLayerIndex(string layer)
        {
            if (int.TryParse(layer, out int index) && index >= 0 && index < 32)
            {
                return index;
            }

            int named = LayerMask.NameToLayer(layer);
            return named;
        }

        #endregion
    }
}