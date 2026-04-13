#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_GameObject
    {
        #region TOOLS METHODS

        /// <summary>
        /// Translates a GameObject by <paramref name="distance"/> units along a named
        /// <paramref name="direction"/>. The orientation frame is resolved from the three
        /// sources below, in priority order:
        /// <list type="number">
        ///   <item>If <paramref name="referenceObject"/> is provided, its transform axes are used.</item>
        ///   <item>If <paramref name="worldSpace"/> is true, world axes are used.</item>
        ///   <item>Otherwise the target GameObject's own transform axes are used.</item>
        /// </list>
        /// The operation is recorded on the Undo stack.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use <paramref name="objectPath"/>.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when <paramref name="instanceId"/> is 0.</param>
        /// <param name="referenceObject">Name or hierarchy path of another GameObject whose axes define the movement frame. When non-empty, overrides <paramref name="worldSpace"/>.</param>
        /// <param name="direction">Named axis direction: forward, back, left, right, up, down.</param>
        /// <param name="distance">Distance to move in Unity units. Default 1.</param>
        /// <param name="worldSpace">When true and no referenceObject is set, moves along world axes. Default true.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the new position, or an error when the target is not found.</returns>
        [McpTool("gameobject-move-relative", Title = "GameObject / Move Relative")]
        [Description("Translates a GameObject in a named direction (forward/back/left/right/up/down) by a given " + "distance. The orientation frame is taken from a reference object, world space, or the object's own " + "transform. Registers the move with Undo.")]
        public ToolResponse MoveRelative(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'World/Player'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Name or hierarchy path of a reference GameObject whose axes define the frame. Empty to use worldSpace.")] string referenceObject = "",
            [Description("Named direction: forward, back, left, right, up, down. Default 'forward'.")] string direction = "forward",
            [Description("Distance to move in Unity units. Default 1.")] float distance = 1f,
            [Description("When true and no referenceObject is set, moves along world axes. Default true.")] bool worldSpace = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"Target GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                Transform? frame = null;
                bool useWorldAxes = false;

                if (!string.IsNullOrWhiteSpace(referenceObject))
                {
                    var refGo = Tool_Transform.FindGameObject(0, referenceObject);

                    if (refGo == null)
                    {
                        return ToolResponse.Error($"Reference GameObject '{referenceObject}' not found.");
                    }

                    frame = refGo.transform;
                }
                else if (worldSpace)
                {
                    useWorldAxes = true;
                }
                else
                {
                    frame = go.transform;
                }

                Vector3 axis;
                string dirNorm = direction.Trim().ToLowerInvariant();

                switch (dirNorm)
                {
                    case "forward":
                        axis = !useWorldAxes && frame != null ? frame.forward : Vector3.forward;
                        break;

                    case "back":
                        axis = !useWorldAxes && frame != null ? -frame.forward : Vector3.back;
                        break;

                    case "left":
                        axis = !useWorldAxes && frame != null ? -frame.right : Vector3.left;
                        break;

                    case "right":
                        axis = !useWorldAxes && frame != null ? frame.right : Vector3.right;
                        break;

                    case "up":
                        axis = !useWorldAxes && frame != null ? frame.up : Vector3.up;
                        break;

                    case "down":
                        axis = !useWorldAxes && frame != null ? -frame.up : Vector3.down;
                        break;

                    default:
                        return ToolResponse.Error($"Unknown direction '{direction}'. Valid values: forward, back, left, right, up, down.");
                }

                Undo.RecordObject(go.transform, $"Move Relative {go.name}");
                go.transform.position += axis * distance;

                return ToolResponse.Text($"Moved '{go.name}' {direction} by {distance} units.\n" + $"  New position: {go.transform.position}");
            });
        }

        #endregion
    }
}