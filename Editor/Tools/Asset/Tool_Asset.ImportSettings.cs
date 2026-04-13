#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region GET IMPORT SETTINGS

        /// <summary>
        /// Reads and returns all serialized importer properties for the given asset.
        /// </summary>
        /// <param name="assetPath">Project-relative path to the asset (e.g. <c>Assets/Textures/Hero.png</c>).</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing every property path, type, and current value
        /// exposed by the asset's <see cref="AssetImporter"/>.
        /// </returns>
        [McpTool("asset-get-import-settings", Title = "Asset / Get Import Settings", ReadOnlyHint = true)]
        [Description("Reads all serialized importer properties for an asset and returns them as text. " + "Use the returned property paths with asset-set-import-settings to change values.")]
        public ToolResponse GetImportSettings(
            [Description("Project-relative asset path (e.g. 'Assets/Textures/Hero.png').")] string assetPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                var importer = AssetImporter.GetAtPath(assetPath);

                if (importer == null)
                {
                    return ToolResponse.Error($"No AssetImporter found for '{assetPath}'. " + "Ensure the path is correct and the asset exists.");
                }

                var serializedObj = new SerializedObject(importer);
                var sb = new StringBuilder();
                sb.AppendLine($"Import Settings — {assetPath}");
                sb.AppendLine($"Importer Type: {importer.GetType().FullName}");
                sb.AppendLine();
                sb.AppendLine("Properties:");

                var iterator = serializedObj.GetIterator();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (iterator.propertyPath == "m_Script")
                    {
                        continue;
                    }

                    sb.AppendLine($"  {iterator.propertyPath} ({iterator.propertyType}): {GetImporterPropertyValueString(iterator)}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region SET IMPORT SETTINGS

        /// <summary>
        /// Applies a set of property overrides to an asset's importer and triggers a reimport.
        /// </summary>
        /// <param name="assetPath">Project-relative path to the asset.</param>
        /// <param name="settingsJson">
        /// JSON object mapping property paths to new string values,
        /// e.g. <c>{"textureType":"1","mipmapEnabled":"true"}</c>.
        /// Values are parsed to the correct serialized type automatically.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming each property that was applied,
        /// or an error message when parsing or application fails.
        /// </returns>
        [McpTool("asset-set-import-settings", Title = "Asset / Set Import Settings")]
        [Description("Applies property overrides to an asset's importer via SerializedObject and triggers SaveAndReimport. " + "settingsJson must be a JSON object mapping property paths to string values " + "(e.g. {\"textureType\":\"1\",\"mipmapEnabled\":\"true\"}).")]
        public ToolResponse SetImportSettings(
            [Description("Project-relative asset path (e.g. 'Assets/Textures/Hero.png').")] string assetPath,
            [Description("JSON object of property-path → value pairs to apply (e.g. {\"textureType\":\"1\"}).")] string settingsJson
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (string.IsNullOrWhiteSpace(settingsJson))
                {
                    return ToolResponse.Error("settingsJson is required. Use asset-get-import-settings to discover property paths.");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                var importer = AssetImporter.GetAtPath(assetPath);

                if (importer == null)
                {
                    return ToolResponse.Error($"No AssetImporter found for '{assetPath}'.");
                }

                string trimmed = settingsJson.Trim();

                if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[^1] != '}')
                {
                    return ToolResponse.Error("settingsJson must be a JSON object starting with '{' and ending with '}'.");
                }

                string inner = trimmed[1..^1].Trim();
                string[] entries = SplitJsonEntries(inner);

                if (entries.Length == 0)
                {
                    return ToolResponse.Error("settingsJson contains no key-value pairs.");
                }

                var serializedObj = new SerializedObject(importer);
                var sb = new StringBuilder();
                sb.AppendLine($"Applied import settings to '{assetPath}':");
                int appliedCount = 0;

                for (int i = 0; i < entries.Length; i++)
                {
                    string entry = entries[i].Trim();

                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        continue;
                    }

                    int colonIndex = entry.IndexOf(':');

                    if (colonIndex < 0)
                    {
                        sb.AppendLine($"  [SKIP] Cannot parse entry: {entry}");
                        continue;
                    }

                    string rawKey   = entry[..colonIndex].Trim().Trim('"');
                    string rawValue = entry[(colonIndex + 1)..].Trim().Trim('"');
                    var prop = serializedObj.FindProperty(rawKey);

                    if (prop == null)
                    {
                        sb.AppendLine($"  [SKIP] Property not found: '{rawKey}'");
                        continue;
                    }

                    bool applied = ApplyStringValueToProperty(prop, rawValue);

                    if (applied)
                    {
                        sb.AppendLine($"  [OK]   {rawKey} = {rawValue}");
                        appliedCount++;
                    }
                    else
                    {
                        sb.AppendLine($"  [SKIP] Could not apply '{rawValue}' to '{rawKey}' ({prop.propertyType})");
                    }
                }

                if (appliedCount == 0)
                {
                    return ToolResponse.Error("No properties were applied. Check property paths with asset-get-import-settings.");
                }

                serializedObj.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();

                sb.AppendLine();
                sb.AppendLine($"SaveAndReimport complete. {appliedCount} propert{(appliedCount == 1 ? "y" : "ies")} applied.");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region IMPORT SETTINGS HELPERS

        /// <summary>
        /// Formats a <see cref="SerializedProperty"/> value as a human-readable string
        /// for display in the get-import-settings response.
        /// </summary>
        /// <param name="prop">The property to format.</param>
        /// <returns>A string representation of the property's current value.</returns>
        private static string GetImporterPropertyValueString(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                SerializedPropertyType.Float => prop.floatValue.ToString("F4"),
                SerializedPropertyType.Boolean => prop.boolValue.ToString(),
                SerializedPropertyType.String => $"\"{prop.stringValue}\"",
                SerializedPropertyType.Enum => prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0 ? prop.enumNames[prop.enumValueIndex] : prop.enumValueIndex.ToString(),
                SerializedPropertyType.Vector2 => prop.vector2Value.ToString(),
                SerializedPropertyType.Vector3 => prop.vector3Value.ToString(),
                SerializedPropertyType.Vector4 => prop.vector4Value.ToString(),
                SerializedPropertyType.Color => prop.colorValue.ToString(),
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})" : "null",
                SerializedPropertyType.ArraySize => prop.intValue.ToString(),
                _ => $"<{prop.propertyType}>",
            };
        }

        /// <summary>
        /// Attempts to apply a string value to a <see cref="SerializedProperty"/> by converting
        /// it to the property's native type.
        /// </summary>
        /// <param name="prop">The property to set.</param>
        /// <param name="value">The string representation of the desired value.</param>
        /// <returns><c>true</c> if the value was successfully applied; <c>false</c> otherwise.</returns>
        private static bool ApplyStringValueToProperty(SerializedProperty prop, string value)
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
                    if (lower == "true" || lower == "1")
                    {
                        prop.boolValue = true;
                        return true;
                    }
                    if (lower == "false" || lower == "0")
                    {
                        prop.boolValue = false;
                        return true;
                    }
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

                default:
                    return false;
            }
        }

        /// <summary>
        /// Splits a flat JSON object body (<c>"key":"val","key2":"val2"</c>) into individual
        /// entry strings, respecting quoted strings so commas inside values are not split on.
        /// </summary>
        /// <param name="jsonBody">The content between the outer braces of a JSON object.</param>
        /// <returns>An array of raw entry strings ready for colon-splitting.</returns>
        private static string[] SplitJsonEntries(string jsonBody)
        {
            var entries = new System.Collections.Generic.List<string>();
            int depth = 0;
            bool inString = false;
            int start = 0;

            for (int i = 0; i < jsonBody.Length; i++)
            {
                char c = jsonBody[i];
                if (c == '\\' && inString)
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    depth--;
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    entries.Add(jsonBody[start..i]);
                    start = i + 1;
                }
            }

            if (start < jsonBody.Length)
            {
                entries.Add(jsonBody[start..]);
            }

            return entries.ToArray();
        }

        #endregion
    }
}