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
        [Description("Applies property overrides to an asset's IMPORTER (not the asset itself) and triggers SaveAndReimport. Use this for importer-level settings such as textureType, mipmapEnabled, compressionQuality. For runtime properties on the asset's loaded UnityEngine.Object (e.g. material.color, texture.wrapMode), use object-modify instead — that writes the asset directly and does not reimport. settingsJson is a JSON object of property paths (discoverable via asset-get-import-settings) mapped to string values (e.g. {\"textureType\":\"1\",\"mipmapEnabled\":\"true\"}).")]
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

        #endregion
    }
}