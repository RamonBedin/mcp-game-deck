#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Scene
    {
        #region TOOL METHODS

        /// <summary>
        /// Frames the specified GameObject in the last active <see cref="SceneView"/>,
        /// computing bounds from all renderers and colliders on the object and its children.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the GameObject to frame. Pass 0 to use <paramref name="objectPath"/>.</param>
        /// <param name="objectPath">Hierarchy path of the GameObject to frame (e.g. 'Level/Boss'). Used when instanceId is 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the framed bounds,
        /// or an error when the GameObject or an active Scene View cannot be located.
        /// </returns>
        [McpTool("scene-view-frame", Title = "Scene / View Frame")]
        [Description("Frames a GameObject in the Scene View by computing bounds from its renderers and colliders. " + "Provide instanceId or objectPath to identify the target.")]
        public ToolResponse ViewFrame(
            [Description("Unity instance ID of the GameObject to frame. Pass 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the GameObject to frame (e.g. 'Level/Boss'). Used when instanceId is 0.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var sv = SceneView.lastActiveSceneView;

                if (sv == null)
                {
                    return ToolResponse.Error("No active Scene View found. Open a Scene View and try again.");
                }

                Bounds bounds = new(go.transform.position, Vector3.zero);
                bool hasBounds = false;
                var renderers = go.GetComponentsInChildren<Renderer>();

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (!hasBounds)
                    {
                        bounds    = renderers[i].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                }

                var colliders = go.GetComponentsInChildren<Collider>();

                for (int i = 0; i < colliders.Length; i++)
                {
                    if (!hasBounds)
                    {
                        bounds    = colliders[i].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(colliders[i].bounds);
                    }
                }

                if (!hasBounds)
                {
                    bounds = new Bounds(go.transform.position, Vector3.one);
                }

                sv.Frame(bounds, false);
                sv.Repaint();

                return ToolResponse.Text($"Framed '{go.name}' in Scene View.\n" + $"  Bounds center: {bounds.center}  size: {bounds.size}");
            });
        }

        #endregion
    }
}