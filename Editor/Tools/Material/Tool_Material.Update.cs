#nullable enable
using System.ComponentModel;
using System.Text;
using System.Globalization;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Material
    {
        #region TOOL METHODS

        /// <summary>
        /// Loads a Material by asset path or name, then applies a set of property overrides
        /// described as a JSON-like key=value string. Supports float, int, and Color (#RRGGBB) values.
        /// </summary>
        /// <param name="assetPath">Direct asset path to the material file (e.g. 'Assets/Materials/Red.mat').</param>
        /// <param name="materialName">Name of the material to search for when assetPath is empty.</param>
        /// <param name="propertiesJson">
        /// Semicolon-separated list of key=value pairs (e.g. "_Metallic=0.5;_BaseColor=#FF0000;_Mode=1").
        /// Color values must be prefixed with '#' in RRGGBB hex format.
        /// </param>
        /// <returns>A <see cref="ToolResponse"/> listing the applied changes, or an error.</returns>
        [McpTool("material-update", Title = "Material / Update")]
        [Description("Updates shader property values on an existing Material asset. " + "Supports float, int, and Color (#RRGGBB) properties via semicolon-separated key=value pairs.")]
        public ToolResponse Update(
            [Description("Asset path of the material to update (e.g. 'Assets/Materials/Red.mat'). Leave empty to search by name.")] string assetPath = "",
            [Description("Name of the material to search for when assetPath is not provided.")] string materialName = "",
            [Description("Semicolon-separated property overrides. Format: 'PropName=Value'. " + "Use # prefix for colors (e.g. '_BaseColor=#FF8800'), plain numbers for float/int (e.g. '_Metallic=0.5;_Mode=1').")] string propertiesJson = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(propertiesJson))
                {
                    return ToolResponse.Error("propertiesJson is required.");
                }

                Material? material = null;

                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    if (!assetPath.StartsWith("Assets/"))
                    {
                        return ToolResponse.Error("assetPath must start with 'Assets/'.");
                    }

                    material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

                    if (material == null)
                    {
                        return ToolResponse.Error($"Material not found at '{assetPath}'.");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(materialName))
                {
                    string[] guids = AssetDatabase.FindAssets($"t:Material {materialName}");

                    for (int i = 0; i < guids.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                        var candidate = AssetDatabase.LoadAssetAtPath<Material>(path);

                        if (candidate != null && candidate.name == materialName)
                        {
                            material = candidate;
                            assetPath = path;
                            break;
                        }
                    }

                    if (material == null)
                    {
                        return ToolResponse.Error($"Material named '{materialName}' not found in the project.");
                    }
                }
                else
                {
                    return ToolResponse.Error("Either assetPath or materialName is required.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Updated material '{material.name}':");

                Undo.RecordObject(material, "Material Update");

                string[] pairs = propertiesJson.Split(';');

                for (int i = 0; i < pairs.Length; i++)
                {
                    string pair = pairs[i].Trim();

                    if (string.IsNullOrWhiteSpace(pair))
                    {
                        continue;
                    }

                    int eqIndex = pair.IndexOf('=');

                    if (eqIndex < 1)
                    {
                        sb.AppendLine($"  [SKIP] '{pair}' — missing '=' separator.");
                        continue;
                    }

                    string propName = pair[..eqIndex].Trim();
                    string propValue = pair[(eqIndex + 1)..].Trim();

                    if (string.IsNullOrWhiteSpace(propName) || string.IsNullOrWhiteSpace(propValue))
                    {
                        sb.AppendLine($"  [SKIP] '{pair}' — empty key or value.");
                        continue;
                    }

                    if (propValue.StartsWith("#"))
                    {
                        string hex = propValue[1..];

                        if (hex.Length >= 6 && int.TryParse(hex[..2],NumberStyles.HexNumber, null, out int r) && int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, null, out int g) && int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, null, out int b))
                        {
                            float a = 1f;

                            if (hex.Length >= 8)
                            {
                                if (!int.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out int av))
                                {
                                    sb.AppendLine($"  [ERROR] Invalid hex color '{propValue}'.");
                                    continue;
                                }

                                a = av / 255f;
                            }

                            var color = new Color(r / 255f, g / 255f, b / 255f, a);
                            material.SetColor(propName, color);
                            sb.AppendLine($"  {propName} = Color({color.r:F3}, {color.g:F3}, {color.b:F3}, {color.a:F3})");
                        }
                        else
                        {
                            sb.AppendLine($"  [ERROR] Invalid hex color '{propValue}'.");
                        }

                        continue;
                    }

                    if (float.TryParse(propValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                    {
                        if (propValue.Contains("."))
                        {
                            material.SetFloat(propName, floatVal);
                            sb.AppendLine($"  {propName} = {floatVal:F4} (float)");
                        }
                        else
                        {
                            int intVal = (int)floatVal;
                            material.SetInt(propName, intVal);
                            sb.AppendLine($"  {propName} = {intVal} (int)");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"  [SKIP] '{propName}' — value '{propValue}' is not a valid number or #RRGGBB color.");
                    }
                }

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}