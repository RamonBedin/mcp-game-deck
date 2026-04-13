#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Light
    {
        #region TOOL METHODS

        /// <summary>
        /// Finds an existing Light component by instance ID or hierarchy path and applies
        /// the supplied property overrides. Only parameters with non-sentinel values are written;
        /// all others are left unchanged. Registers the change with Undo.
        /// </summary>
        /// <param name="instanceId">Instance ID of the GameObject that has the Light component. 0 to skip.</param>
        /// <param name="objectPath">Hierarchy path of the GameObject (e.g. "Scene/Lights/Sun"). Empty to skip.</param>
        /// <param name="intensity">New intensity value. -1 to leave unchanged.</param>
        /// <param name="color">Hex color string. Empty to leave unchanged.</param>
        /// <param name="range">Light range (Point/Spot only). -1 to leave unchanged.</param>
        /// <param name="spotAngle">Spot angle in degrees (Spot only). -1 to leave unchanged.</param>
        /// <param name="shadows">Shadow mode: "None", "Hard", or "Soft". Empty to leave unchanged.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each property that was changed, or an error when
        /// the target GameObject cannot be found or has no Light component.
        /// </returns>
        [McpTool("light-configure", Title = "Light / Configure")]
        [Description("Configures an existing Light component identified by instanceId or objectPath. " + "Only supplied (non-sentinel) values are applied. " + "intensity: -1 = skip. color: '' = skip. range: -1 = skip. " + "spotAngle: -1 = skip. shadows: '' = skip, or 'None'/'Hard'/'Soft'.")]
        public ToolResponse ConfigureLight(
            [Description("Instance ID of the target GameObject. Use 0 to locate by objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Lights/Sun'). " + "Used when instanceId is 0.")] string objectPath = "",
            [Description("New intensity. -1 to leave unchanged.")] float intensity = -1f,
            [Description("Hex color string (e.g. '#FF8800'). Empty to leave unchanged.")] string color = "",
            [Description("Light range for Point and Spot lights. -1 to leave unchanged.")] float range = -1f,
            [Description("Spot angle in degrees for Spot lights. -1 to leave unchanged.")] float spotAngle = -1f,
            [Description("Shadow mode: 'None', 'Hard', or 'Soft'. Empty to leave unchanged.")] string shadows = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    if (instanceId != 0)
                    {
                        return ToolResponse.Error($"No GameObject found for instanceId {instanceId}.");
                    }
                    if (!string.IsNullOrWhiteSpace(objectPath))
                    {
                        return ToolResponse.Error($"No GameObject found at path '{objectPath}'.");
                    }

                    return ToolResponse.Error("Provide instanceId or objectPath to identify the target GameObject.");
                }

                if (!go.TryGetComponent<Light>(out var light))
                {
                    return ToolResponse.Error($"GameObject '{go.name}' has no Light component.");
                }

                if (spotAngle >= 0f && (spotAngle < 1f || spotAngle > 179f))
                {
                    return ToolResponse.Error("spotAngle must be between 1 and 179 degrees.");
                }

                Undo.RecordObject(light, $"Configure Light {go.name}");

                var sb = new StringBuilder();
                sb.AppendLine($"Configured light '{go.name}':");

                if (intensity >= 0f)
                {
                    light.intensity = intensity;
                    sb.AppendLine($"  Intensity: {intensity}");
                }

                if (!string.IsNullOrWhiteSpace(color))
                {
                    if (!TryParseHexColor(color, out Color parsedColor))
                    {
                        return ToolResponse.Error($"Cannot parse color '{color}'. Use hex format: '#FFFFFF'.");
                    }

                    light.color = parsedColor;
                    sb.AppendLine($"  Color: {light.color}");
                }

                if (range >= 0f)
                {
                    if (range == 0f)
                    {
                        sb.AppendLine("  [WARNING] range = 0 is invalid for Point/Spot lights.");
                    }

                    light.range = range;
                    sb.AppendLine($"  Range: {range}");
                }

                if (spotAngle >= 0f)
                {
                    light.spotAngle = spotAngle;
                    sb.AppendLine($"  Spot Angle: {spotAngle}");
                }

                if (!string.IsNullOrWhiteSpace(shadows))
                {
                    switch (shadows.Trim().ToLowerInvariant())
                    {
                        case "none":
                            light.shadows = LightShadows.None;
                            break;

                        case "hard":
                            light.shadows = LightShadows.Hard;
                            break;

                        case "soft":
                            light.shadows = LightShadows.Soft;
                            break;

                        default:
                            return ToolResponse.Error($"Unknown shadows value '{shadows}'. Valid values: None, Hard, Soft.");
                    }

                    sb.AppendLine($"  Shadows: {light.shadows}");
                }

                EditorUtility.SetDirty(light);
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}