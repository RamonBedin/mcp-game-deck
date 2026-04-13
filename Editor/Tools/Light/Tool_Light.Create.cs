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
    /// <summary>
    /// MCP tools for creating, configuring, and querying Light components in the Unity scene.
    /// Covers point, directional, spot, and area light creation, property configuration,
    /// and full-scene light enumeration.
    /// </summary>
    [McpToolType]
    public partial class Tool_Light
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new GameObject with a Light component and configures its type,
        /// position, intensity, and color. Registers the operation with Undo.
        /// </summary>
        /// <param name="lightType">Light type: "Directional", "Point", "Spot", or "Area". Default "Point".</param>
        /// <param name="name">Name for the new GameObject. Defaults to the light type name.</param>
        /// <param name="posX">World-space X position. Default 0.</param>
        /// <param name="posY">World-space Y position. Default 0.</param>
        /// <param name="posZ">World-space Z position. Default 0.</param>
        /// <param name="intensity">Light intensity. Default 1.</param>
        /// <param name="color">Hex color string (e.g. "#FFFFFF" or "FF8800"). Default "#FFFFFF".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with a summary of the created light, or an error
        /// when the lightType is unknown or the color string cannot be parsed.
        /// </returns>
        [McpTool("light-create", Title = "Light / Create")]
        [Description("Creates a new Light GameObject in the scene. " + "Supports Directional, Point, Spot, and Area light types. " + "Color is specified as a hex string (e.g. '#FFFFFF'). " + "The operation is registered with Undo.")]
        public ToolResponse CreateLight(
            [Description("Light type: 'Directional', 'Point', 'Spot', or 'Area'. Default 'Point'.")] string lightType = "Point",
            [Description("Name for the new GameObject. Defaults to the light type (e.g. 'Point Light').")] string name = "",
            [Description("World-space X position. Default 0.")] float posX = 0f,
            [Description("World-space Y position. Default 0.")] float posY = 0f,
            [Description("World-space Z position. Default 0.")] float posZ = 0f,
            [Description("Light intensity. Default 1.")] float intensity = 1f,
            [Description("Hex color string (e.g. '#FFFFFF', 'FF8800'). Default '#FFFFFF'.")] string color = "#FFFFFF"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                LightType unityLightType;

                switch (lightType.Trim().ToLowerInvariant())
                {
                    case "directional":
                        unityLightType = LightType.Directional;
                        break;

                    case "point":
                        unityLightType = LightType.Point;
                        break;

                    case "spot":
                        unityLightType = LightType.Spot;
                        break;

                    case "area":
                        unityLightType = LightType.Rectangle;
                        break;

                    default:
                        return ToolResponse.Error($"Unknown lightType '{lightType}'. Valid values: Directional, Point, Spot, Area.");
                }

                if (!TryParseHexColor(color, out Color lightColor))
                {
                    return ToolResponse.Error($"Cannot parse color '{color}'. Use hex format: '#FFFFFF' or 'FF8800'.");
                }

                string goName = string.IsNullOrWhiteSpace(name) ? $"{lightType} Light" : name;

                var go = new GameObject(goName);
                go.transform.position = new Vector3(posX, posY, posZ);

                var light = go.AddComponent<Light>();
                light.type      = unityLightType;
                light.intensity = intensity;
                light.color     = lightColor;

                Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");
                Selection.activeGameObject = go;

                var sb = new StringBuilder();
                sb.AppendLine($"Created light '{goName}':");
                sb.AppendLine($"  Type:      {unityLightType}");
                sb.AppendLine($"  Position:  ({posX}, {posY}, {posZ})");
                sb.AppendLine($"  Intensity: {intensity}");
                sb.AppendLine($"  Color:     {lightColor}");
                sb.AppendLine($"  InstanceId: {go.GetInstanceID()}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Attempts to parse a hex color string (with or without leading '#') into a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">Hex color string (e.g. "#FFFFFF", "FF8800", "#FF8800FF").</param>
        /// <param name="result">Parsed color. <see cref="Color.white"/> on failure.</param>
        /// <returns><c>true</c> when parsing succeeded; otherwise <c>false</c>.</returns>
        private static bool TryParseHexColor(string hex, out Color result)
        {
            result = Color.white;

            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            string h = hex.Trim();

            if (!h.StartsWith("#"))
            {
                h = "#" + h;
            }

            return ColorUtility.TryParseHtmlString(h, out result);
        }

        #endregion
    }
}