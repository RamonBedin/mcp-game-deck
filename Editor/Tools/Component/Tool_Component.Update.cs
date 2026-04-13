#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Component
    {
        #region TOOL METHODS

        /// <summary>
        /// Updates one or more serialized properties on a Component using a JSON property map.
        /// Supports float, int, bool, and string property types.
        /// All changes are applied through <see cref="SerializedObject"/> for full Undo support.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <param name="componentType">
        /// Simple or fully-qualified type name of the component to modify
        /// (e.g. "Rigidbody", "BoxCollider", "Light").
        /// </param>
        /// <param name="propertiesJson">
        /// JSON object mapping serialized property names to their new values.
        /// Example: <c>{"mass":5.0,"useGravity":true,"tag":"Player"}</c>.
        /// Supports float, int, bool, and string values.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each property that was set successfully,
        /// or an error if the GameObject, component, or any property could not be resolved.
        /// </returns>
        [McpTool("component-update", Title = "Component / Update Properties")]
        [Description("Updates serialized properties on an existing Component using a JSON property map. " + "Supports float, int, bool, and string value types. " + "Changes are applied via SerializedObject for full Undo support. " + "Example propertiesJson: {\"mass\":5.0,\"useGravity\":true}.")]
        public ToolResponse UpdateComponentProperties(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Simple or fully-qualified component type name to modify " +"(e.g. 'Rigidbody', 'BoxCollider', 'Light').")] string componentType = "",
            [Description("JSON object mapping serialized property names to new values. " + "Supports float, int, bool, and string. Example: {\"mass\":5.0,\"useGravity\":true}.")] string propertiesJson = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(componentType))
                {
                    return ToolResponse.Error("componentType is required.");
                }

                if (string.IsNullOrWhiteSpace(propertiesJson))
                {
                    return ToolResponse.Error("propertiesJson is required.");
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var resolvedType = ResolveComponentType(componentType);

                if (resolvedType == null)
                {
                    return ToolResponse.Error($"Could not resolve component type '{componentType}'.");
                }

                var component = go.GetComponent(resolvedType);

                if (component == null)
                {
                    return ToolResponse.Error($"Component '{resolvedType.FullName}' not found on '{go.name}'.");
                }

                var pairs = ParseJsonObject(propertiesJson);

                if (pairs == null)
                {
                    return ToolResponse.Error("Failed to parse propertiesJson. Ensure it is a flat JSON object " + "with string keys and scalar values (float, int, bool, string).");
                }

                var serializedObj = new SerializedObject(component);
                var sb = new StringBuilder();
                var warnSb = new StringBuilder();
                int setCount = 0;

                for (int i = 0; i < pairs.Length; i++)
                {
                    string key   = pairs[i]._key;
                    string value = pairs[i]._value;

                    var prop = serializedObj.FindProperty(key);

                    if (prop == null)
                    {
                        warnSb.AppendLine($"  [not found] {key}");
                        continue;
                    }

                    bool applied = ApplyPropertyValue(prop, value);

                    if (applied)
                    {
                        sb.AppendLine($"  {key} = {value}");
                        setCount++;
                    }
                    else
                    {
                        warnSb.AppendLine($"  [unsupported type '{prop.propertyType}'] {key}");
                    }
                }

                if (setCount > 0)
                {
                    serializedObj.ApplyModifiedProperties();
                }

                var result = new StringBuilder();
                result.AppendLine($"Updated {setCount} property(ies) on '{resolvedType.Name}' ({go.name}):");
                result.Append(sb);

                if (warnSb.Length > 0)
                {
                    result.AppendLine("Skipped:");
                    result.Append(warnSb);
                }

                return ToolResponse.Text(result.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// A lightweight key-value pair used to represent one JSON property entry.
        /// Both key and value are stored as raw strings; value parsing is deferred to
        /// <see cref="ApplyPropertyValue"/>.
        /// </summary>
        private struct JsonPair
        {
            public string _key;
            public string _value;
        }

        /// <summary>
        /// Parses a flat JSON object into an array of <see cref="JsonPair"/> entries without
        /// using any external JSON library or System.Linq.
        /// Only handles scalar values: numbers, booleans, and quoted strings.
        /// Nested objects and arrays are not supported.
        /// </summary>
        /// <param name="json">The JSON string to parse. Must be a flat object literal.</param>
        /// <returns>
        /// An array of <see cref="JsonPair"/> on success, or <c>null</c> if parsing fails
        /// due to malformed input.
        /// </returns>
        private static JsonPair[]? ParseJsonObject(string json)
        {
            json = json.Trim();

            if (json.Length < 2 || json[0] != '{' || json[^1] != '}')
            {
                return null;
            }

            json = json[1..^1].Trim();

            if (json.Length == 0)
            {
                return System.Array.Empty<JsonPair>();
            }

            int commaCount = 0;

            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == ',')
                {
                    commaCount++;
                }
            }

            var result = new JsonPair[commaCount + 1];
            int resultCount = 0;

            int pos = 0;

            while (pos < json.Length)
            {
                while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n'))
                {
                    pos++;
                }

                if (pos >= json.Length)
                {
                    break;
                }

                if (json[pos] != '"')
                {
                    return null;
                }

                pos++;
                int keyStart = pos;

                while (pos < json.Length && json[pos] != '"')
                {
                    pos++;
                }

                if (pos >= json.Length)
                {
                    return null;
                }

                string key = json[keyStart..pos];
                pos++;

                while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t'))
                {
                    pos++;
                }

                if (pos >= json.Length || json[pos] != ':')
                {
                    return null;
                }

                pos++;

                while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t'))
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
                    pos++;
                    int valStart = pos;

                    while (pos < json.Length && json[pos] != '"')
                    {
                        if (json[pos] == '\\')
                        {
                            pos++;
                        }

                        pos++;
                    }

                    if (pos >= json.Length)
                    {
                        return null;
                    }

                    value = json[valStart..pos];
                    pos++;
                }
                else
                {
                    int valStart = pos;

                    while (pos < json.Length && json[pos] != ',' && json[pos] != '}' && json[pos] != ' ' && json[pos] != '\t' && json[pos] != '\r' && json[pos] != '\n')
                    {
                        pos++;
                    }

                    value = json[valStart..pos];
                }

                result[resultCount]._key = key;
                result[resultCount]._value = value;
                resultCount++;

                while (pos < json.Length && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n'))
                {
                    pos++;
                }

                if (pos < json.Length && json[pos] == ',')
                {
                    pos++;
                }
            }

            var trimmed = new JsonPair[resultCount];

            for (int i = 0; i < resultCount; i++)
            {
                trimmed[i] = result[i];
            }

            return trimmed;
        }

        /// <summary>
        /// Attempts to apply a raw string value to a <see cref="SerializedProperty"/>.
        /// Supports <see cref="SerializedPropertyType.Float"/>,
        /// <see cref="SerializedPropertyType.Integer"/>,
        /// <see cref="SerializedPropertyType.Boolean"/>, and
        /// <see cref="SerializedPropertyType.String"/> property types.
        /// </summary>
        /// <param name="prop">The serialized property to modify.</param>
        /// <param name="rawValue">The raw string token from the parsed JSON.</param>
        /// <returns><c>true</c> if the value was set; <c>false</c> if the property type is unsupported.</returns>
        private static bool ApplyPropertyValue(SerializedProperty prop, string rawValue)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    if (float.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fVal))
                    {
                        prop.floatValue = fVal;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Integer:
                    if (int.TryParse(rawValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int iVal))
                    {
                        prop.intValue = iVal;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Boolean:
                    string lower = rawValue.ToLowerInvariant();
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
                    prop.stringValue = rawValue;
                    return true;

                default:
                    return false;
            }
        }

        #endregion
    }
}