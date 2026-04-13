#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Light
    {
        #region TOOL METHODS

        /// <summary>
        /// Finds every Light component in the active scene (including inactive GameObjects)
        /// and returns their key properties.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each light's GameObject name, instance ID,
        /// light type, intensity, color, enabled state, range, spot angle (Spot only),
        /// and shadow mode. Returns an informational message when no lights are found.
        /// </returns>
        [McpTool("light-list", Title = "Light / List", ReadOnlyHint = true)]
        [Description("Lists all Light components in the active scene including inactive GameObjects. " + "Returns name, instanceId, type, intensity, color, enabled, range, spotAngle (Spot only), " + "and shadow mode for each light found.")]
        public ToolResponse ListLights()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var scene = SceneManager.GetActiveScene();
                var roots = scene.GetRootGameObjects();
                var lights = new System.Collections.Generic.List<Light>();

                for (int i = 0; i < roots.Length; i++)
                {
                    CollectLightsRecursive(roots[i], lights);
                }

                if (lights.Count == 0)
                {
                    return ToolResponse.Text("No Light components found in the active scene.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Lights in scene '{scene.name}' ({lights.Count}):");

                for (int i = 0; i < lights.Count; i++)
                {
                    var light = lights[i];
                    var go    = light.gameObject;

                    sb.AppendLine($"  {go.name} (instanceId: {go.GetInstanceID()}):");
                    sb.AppendLine($"    Type:      {light.type}");
                    sb.AppendLine($"    Intensity: {light.intensity}");
                    sb.AppendLine($"    Color:     #{ColorToHex(light.color)}");
                    sb.AppendLine($"    Enabled:   {light.enabled && go.activeInHierarchy}");
                    sb.AppendLine($"    Shadows:   {light.shadows}");

                    if (light.type != LightType.Directional)
                    {
                        sb.AppendLine($"    Range:     {light.range}");
                    }

                    if (light.type == LightType.Spot)
                    {
                        sb.AppendLine($"    SpotAngle: {light.spotAngle}");
                    }

                    sb.AppendLine();
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Recursively collects all <see cref="Light"/> components starting from <paramref name="go"/>.
        /// </summary>
        /// <param name="go">Root GameObject to begin traversal from.</param>
        /// <param name="results">List to append found lights into.</param>
        private static void CollectLightsRecursive(GameObject go, System.Collections.Generic.List<Light> results)
        {
            if (go.TryGetComponent<Light>(out var light))
            {
                results.Add(light);
            }

            var t = go.transform;

            for (int i = 0; i < t.childCount; i++)
            {
                CollectLightsRecursive(t.GetChild(i).gameObject, results);
            }
        }

        /// <summary>
        /// Converts a <see cref="Color"/> to an uppercase 6-character hex string (RGB, no alpha).
        /// </summary>
        /// <param name="c">Color to convert.</param>
        /// <returns>Uppercase hex string such as "FF8800".</returns>
        private static string ColorToHex(Color c)
        {
            int r = Mathf.RoundToInt(c.r * 255f);
            int g = Mathf.RoundToInt(c.g * 255f);
            int b = Mathf.RoundToInt(c.b * 255f);
            return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
        }

        #endregion
    }
}