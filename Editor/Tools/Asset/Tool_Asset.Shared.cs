#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region SHARED PARSER

        /// <summary>
        /// Attempts to apply a string value to a <see cref="SerializedProperty"/> by converting
        /// it to the property's native type.
        /// Shared across Asset-domain tools that write to SerializedObject (currently asset-set-import-settings).
        /// Supports Integer, Float, Boolean, String, Enum, Color, and ObjectReference (treats input as asset path).
        /// </summary>
        /// <param name="prop">The property to set.</param>
        /// <param name="value">The string representation of the desired value. For ObjectReference props, this is a project-relative asset path.</param>
        /// <returns><c>true</c> if the value was successfully applied; <c>false</c> if the value could not be parsed or the property type is unsupported.</returns>
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

                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(value, out Color color))
                    {
                        prop.colorValue = color;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.ObjectReference:
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        prop.objectReferenceValue = null;
                        return true;
                    }

                    string assetPath = value;

                    if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                    {
                        assetPath = "Assets/" + assetPath;
                    }

                    var loaded = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                    if (loaded == null)
                    {
                        return false;
                    }

                    prop.objectReferenceValue = loaded;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Splits a flat JSON object body (<c>"key":"val","key2":"val2"</c>) into individual
        /// entry strings, respecting quoted strings and nested braces so commas inside values are not split on.
        /// </summary>
        /// <param name="jsonBody">The content between the outer braces of a JSON object.</param>
        /// <returns>An array of raw entry strings ready for colon-splitting.</returns>
        private static string[] SplitJsonEntries(string jsonBody)
        {
            var entries = new List<string>();
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