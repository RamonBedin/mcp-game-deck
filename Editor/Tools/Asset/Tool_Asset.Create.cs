#nullable enable
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using SimpleJSON;
using System.ComponentModel;
using System.IO;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.GraphicsBuffer;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region CREATE

        /// <summary>
        /// Creates a new Unity asset at the specified path.
        /// Supported types: Material, RenderTexture, PhysicMaterial, AnimatorController.
        /// </summary>
        /// <param name="path">Project-relative asset path including file name and extension (e.g. 'Assets/Mats/Rock.mat').</param>
        /// <param name="assetType">Type of asset to create: "Material", "RenderTexture", "PhysicMaterial", or "AnimatorController".</param>
        /// <param name="propertiesJson">Optional JSON object with additional properties (reserved for future use).</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the created asset path,
        /// or an error when the type is unsupported or the asset cannot be saved.
        /// </returns>
        [McpTool("asset-create", Title = "Asset / Create")]
        [Description("Creates a new Unity asset at the given path. " + "assetType values: 'Material', 'RenderTexture', 'PhysicMaterial', 'AnimatorController'. " + "path must include the file name and correct extension.")]
        public ToolResponse Create(
            [Description("Project-relative path including file name and extension (e.g. 'Assets/Materials/Rock.mat').")] string path,
            [Description("Type of asset to create: 'Material', 'RenderTexture', 'PhysicMaterial', or 'AnimatorController'. Default 'Material'.")] string assetType = "Material",
            [Description("Optional JSON object with additional initial properties. Currently reserved for future use.")] string propertiesJson = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    path = "Assets/" + path;
                }

                string folder = Path.GetDirectoryName(path) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                path = AssetDatabase.GenerateUniqueAssetPath(path);

                switch (assetType.ToLowerInvariant())
                {
                    case "material":
                    {
                        var shader = Shader.Find("Standard");
                        if (shader == null)
                        {
                            return ToolResponse.Error("Shader 'Standard' not found in the project.");
                        }

                        var mat = new Material(shader);
                        AssetDatabase.CreateAsset(mat, path);
                        string matProps = ApplyPropertiesFromJson(mat, propertiesJson);
                        AssetDatabase.SaveAssets();
                        return ToolResponse.Text($"Material created at '{path}'.{matProps}");
                     }

                    case "rendertexture":
                    {
                        var rt = new RenderTexture(256, 256, 24);
                        AssetDatabase.CreateAsset(rt, path);
                        AssetDatabase.SaveAssets();
                        return ToolResponse.Text($"RenderTexture (256x256, depth 24) created at '{path}'.");
                    }

                    case "physicmaterial":
                    {
                        var pm = new PhysicsMaterial();
                        AssetDatabase.CreateAsset(pm, path);
                        string pmProps = ApplyPropertiesFromJson(pm, propertiesJson);
                        AssetDatabase.SaveAssets();
                        return ToolResponse.Text($"PhysicMaterial created at '{path}'.{pmProps}");
                    }

                    case "animatorcontroller":
                    {
                        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
                        if (ctrl == null)
                        {
                            return ToolResponse.Error($"Failed to create AnimatorController at '{path}'.");
                        }

                        AssetDatabase.SaveAssets();
                        return ToolResponse.Text($"AnimatorController created at '{path}'.");
                    }

                    default:
                        return ToolResponse.Error($"Unsupported assetType '{assetType}'. " + "Valid values: 'Material', 'RenderTexture', 'PhysicMaterial', 'AnimatorController'.");
                }
            });
        }

        #endregion

        #region PROPERTIES HELPER

        /// <summary>
        /// Parses a JSON object of property overrides and applies them to a Unity Object
        /// via <see cref="SerializedObject"/>. Supports int, float, bool, string, enum,
        /// color, and vector properties.
        /// </summary>
        /// <param name="target">The asset to apply properties to.</param>
        /// <param name="json">JSON string with property-path → value pairs, or empty to skip.</param>
        /// <returns>A summary string of applied properties (prefixed with newline), or empty if none.</returns>
        private static string ApplyPropertiesFromJson(Object target, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "";
            }

            string trimmed = json.Trim();

            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}')
            {
                return "\nWarning: propertiesJson must be a JSON object — skipped.";
            }

            var so = new SerializedObject(target);
            var sb = new System.Text.StringBuilder();
            sb.Append("\nProperties applied:");
            int applied = 0;

            string inner = trimmed[1..^1].Trim();
            int start = 0;
            bool inString = false;

            for (int i = 0; i <= inner.Length; i++)
            {
                if (i < inner.Length && inner[i] == '\\' && inString)
                {
                    i++; continue;
                }

                if (i < inner.Length && inner[i] == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (i == inner.Length || inner[i] == ',')
                {
                    string entry = inner[start..i].Trim();
                    start = i + 1;

                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        continue;
                    }

                    int colon = entry.IndexOf(':');

                    if (colon < 0)
                    {
                        continue;
                    }

                    string key = entry[..colon].Trim().Trim('"');
                    string val = entry[(colon + 1)..].Trim().Trim('"');
                    var prop = so.FindProperty(key);

                    if (prop == null)
                    {
                        sb.Append($"\n  [SKIP] '{key}' not found");
                        continue;
                    }

                    if (ApplyValue(prop, val))
                    {
                        sb.Append($"\n  [OK] {key} = {val}");
                        applied++;
                    }
                    else
                    {
                        sb.Append($"\n  [SKIP] '{key}' ({prop.propertyType}) could not apply '{val}'");
                    }
                }
            }

            if (applied > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }

            return applied > 0 ? sb.ToString() : "";
        }

        /// <summary>
        /// Applies a string value to a <see cref="SerializedProperty"/> by converting
        /// to the property's native type.
        /// </summary>
        /// <param name="prop">The property to set.</param>
        /// <param name="value">The string representation of the value.</param>
        /// <returns><c>true</c> if the value was applied; <c>false</c> otherwise.</returns>
        private static bool ApplyValue(SerializedProperty prop, string value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (int.TryParse(value, out int intVal))
                    {
                        prop.intValue = intVal;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Float:
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatVal))
                    {
                        prop.floatValue = floatVal;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Boolean:
                    string lower = value.ToLowerInvariant();
                    if (lower == "true" || lower == "1") { prop.boolValue = true; return true; }
                    if (lower == "false" || lower == "0") { prop.boolValue = false; return true; }
                    return false;

                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    return true;

                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out int enumInt))
                    {
                        prop.enumValueIndex = enumInt;
                        return true;
                    }
                    for (int i = 0; i < prop.enumNames.Length; i++)
                    {
                        if (string.Compare(prop.enumNames[i], value, System.StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            prop.enumValueIndex = i;
                            return true;
                        }
                    }
                    return false;

                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(value, out Color color))
                    {
                        prop.colorValue = color;
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        #endregion
    }
}