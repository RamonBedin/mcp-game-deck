#pragma warning disable CS0618
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
    public partial class Tool_Object
    {
        #region TOOL METHODS

        /// <summary>
        /// Resolves a Unity object by instance ID, parses a JSON object of property-path-to-value
        /// mappings, and applies each change via <see cref="SerializedObject"/>.
        /// Supports int, float, bool, string, Vector2, Vector3, Color, and object-reference properties.
        /// </summary>
        /// <param name="instanceId">Instance ID of the Unity object to modify.</param>
        /// <param name="propertiesJson">
        /// JSON object mapping serialized property paths to new values.
        /// Example: {"damage":25,"label":"Boss","color":"1,0,0,1"}
        /// </param>
        /// <returns>A <see cref="ToolResponse"/> summarising applied changes, or an error message.</returns>
        [McpTool("object-modify", Title = "Object / Modify")]
        [Description("Modifies multiple serialized properties on any Unity object identified by instance ID. " + "propertiesJson is a flat JSON object mapping property paths to string-encoded values. " + "Supports int, float, bool, string, enum (by index or name), Vector2/3, Color, and object references (by asset path).")]
        public ToolResponse Modify(
            [Description("Instance ID of the Unity object to modify.")] int instanceId,
            [Description("JSON object mapping serialized property paths to new string-encoded values. " + "Example: {\"damage\":25,\"label\":\"Boss\",\"tint\":\"1,0,0,1\"}")] string propertiesJson
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (instanceId == 0)
                {
                    return ToolResponse.Error("instanceId must not be 0.");
                }

                if (string.IsNullOrWhiteSpace(propertiesJson))
                {
                    return ToolResponse.Error("propertiesJson is required.");
                }

                var obj = EditorUtility.InstanceIDToObject(instanceId);

                if (obj == null)
                {
                    return ToolResponse.Error($"No object found with instanceId {instanceId}.");
                }

                var pairs = ParseFlatJson(propertiesJson);

                if (pairs == null)
                {
                    return ToolResponse.Error("propertiesJson could not be parsed. Ensure it is a flat JSON object.");
                }

                Undo.RecordObject(obj, "Object / Modify");
                var serializedObj = new SerializedObject(obj);
                var sb = new StringBuilder();
                int appliedCount = 0;

                for (int i = 0; i < pairs.Count; i++)
                {
                    string propertyPath = pairs[i].Key;
                    string value = pairs[i].Value;
                    var prop = serializedObj.FindProperty(propertyPath);

                    if (prop == null)
                    {
                        sb.AppendLine($"  SKIP '{propertyPath}': property not found.");
                        continue;
                    }

                    bool success = ApplyPropertyValue(prop, value);

                    if (success)
                    {
                        sb.AppendLine($"  SET '{propertyPath}' = '{value}'.");
                        appliedCount++;
                    }
                    else
                    {
                        sb.AppendLine($"  FAIL '{propertyPath}' (type: {prop.propertyType}): cannot parse '{value}'.");
                    }
                }

                serializedObj.ApplyModifiedProperties();
                EditorUtility.SetDirty(obj);

                string summary = $"Modified {appliedCount}/{pairs.Count} properties on '{obj.name}' ({obj.GetType().Name}):\n";
                return ToolResponse.Text(summary + sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Applies a string-encoded value to a <see cref="SerializedProperty"/>.
        /// Supports int, float, bool, string, enum, Vector2, Vector3, Color, and object references.
        /// </summary>
        /// <param name="prop">Property to modify.</param>
        /// <param name="value">String-encoded value.</param>
        /// <returns>True if the value was applied successfully.</returns>
        private static bool ApplyPropertyValue(SerializedProperty prop, string value)
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
                    if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                    {
                        prop.floatValue = floatVal;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.ToLowerInvariant() == "true" || value == "1";
                    return true;

                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    return true;

                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out int enumIdx))
                    {
                        prop.enumValueIndex = enumIdx;
                        return true;
                    }
                    string[] names = prop.enumNames;
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (names[i].Equals(value, System.StringComparison.OrdinalIgnoreCase))
                        {
                            prop.enumValueIndex = i;
                            return true;
                        }
                    }
                    return false;

                case SerializedPropertyType.Vector2:
                {
                    string[] parts = value.Split(',');
                    if (parts.Length >= 2 && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    {
                        prop.vector2Value = new Vector2(x, y);
                        return true;
                    }
                    return false;
                }

                case SerializedPropertyType.Vector3:
                {
                    string[] parts = value.Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) && float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        prop.vector3Value = new Vector3(x, y, z);
                        return true;
                    }
                    return false;
                }

                case SerializedPropertyType.Color:
                {
                    string[] parts = value.Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float r) && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float g) && float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
                    {
                        float a = 1f;
                        if (parts.Length >= 4)
                        {
                            float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a);
                        }
                        prop.colorValue = new Color(r, g, b, a);
                        return true;
                    }
                    return false;
                }

                case SerializedPropertyType.ObjectReference:
                {
                    var referenced = AssetDatabase.LoadAssetAtPath<Object>(value);
                    if (referenced == null)
                    {
                        return false;
                    }
                    prop.objectReferenceValue = referenced;
                    return true;
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// Parses a flat JSON object string into an ordered list of key-value string pairs.
        /// Handles both quoted string values and unquoted numeric/boolean values.
        /// Does not support nested objects or arrays.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        /// <returns>List of key-value pairs, or null if parsing fails.</returns>
        private static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>? ParseFlatJson(string json)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();

            json = json.Trim();

            if (!json.StartsWith("{") || !json.EndsWith("}"))
            {
                return null;
            }

            json = json[1..^1].Trim();

            if (json.Length == 0)
            {
                return result;
            }

            int pos = 0;

            while (pos < json.Length)
            {
                while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n')) pos++;

                if (pos >= json.Length)
                {
                    break;
                }

                if (json[pos] != '"')
                {
                    return null;
                }

                string? key = ReadQuotedString(json, ref pos);

                if (key == null)
                {
                    return null;
                }

                while (pos < json.Length && json[pos] != ':')
                {
                    pos++;
                }

                pos++;

                while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n'))
                {
                    pos++;
                }

                if (pos >= json.Length)
                {
                    return null;
                }

                string value;

                if (json[pos] == '"')
                {
                    string? sv = ReadQuotedString(json, ref pos);

                    if (sv == null)
                    {
                        return null;
                    }

                    value = sv;
                }
                else
                {
                    int start = pos;

                    while (pos < json.Length && json[pos] != ',' && json[pos] != '}')
                    {
                        pos++;
                    }

                    value = json[start..pos].Trim();
                }

                result.Add(new System.Collections.Generic.KeyValuePair<string, string>(key, value));

                while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n'))
                {
                    pos++;
                }

                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                }
            }

            return result;
        }

        /// <summary>
        /// Reads a JSON-quoted string from <paramref name="json"/> starting at <paramref name="pos"/>
        /// (which must point at the opening quote), advancing pos past the closing quote.
        /// </summary>
        /// <param name="json">Source JSON string.</param>
        /// <param name="pos">Current parse position; updated on return.</param>
        /// <returns>The unescaped string content, or null if malformed.</returns>
        private static string? ReadQuotedString(string json, ref int pos)
        {
            if (pos >= json.Length || json[pos] != '"')
            {
                return null;
            }

            pos++;
            var sb = new StringBuilder();

            while (pos < json.Length && json[pos] != '"')
            {
                if (json[pos] == '\\' && pos + 1 < json.Length)
                {
                    pos++;

                    switch (json[pos])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case '/':  sb.Append('/');  break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(json[pos]); break;
                    }
                }
                else
                {
                    sb.Append(json[pos]);
                }

                pos++;
            }

            if (pos >= json.Length)
            {
                return null;
            }

            pos++;
            return sb.ToString();
        }

        #endregion
    }
}