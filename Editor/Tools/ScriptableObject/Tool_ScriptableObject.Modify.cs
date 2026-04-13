#nullable enable
using System.ComponentModel;
using System.Globalization;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_ScriptableObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Modifies a serialized property on an existing ScriptableObject asset using
        /// SerializedObject property paths. Supports int, float, string, bool, Vector2, Vector3,
        /// Color, and object reference fields.
        /// </summary>
        /// <param name="assetPath">Asset path of the ScriptableObject (e.g. <c>Assets/Data/Weapons/Sword.asset</c>).</param>
        /// <param name="propertyPath">
        /// SerializedProperty path (e.g. <c>damage</c>, <c>stats.health</c>, <c>items.Array.data[0]</c>).
        /// </param>
        /// <param name="value">
        /// Value to set as a string. Use the raw number for numeric types, <c>true</c>/<c>false</c>
        /// for booleans, <c>x,y,z</c> for vectors, <c>r,g,b,a</c> (0-1 range) for colors,
        /// or an asset path for object references.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the property was set, or an error when the
        /// asset or property cannot be found or the value cannot be parsed for the property's type.
        /// </returns>
        [McpTool("scriptableobject-modify", Title = "ScriptableObject / Modify Property")]
        [Description("Modifies a serialized property on an existing ScriptableObject asset using " + "SerializedObject property paths. Supports int, float, string, bool, Vector2, Vector3, Color, " + "and object reference fields.")]
        public ToolResponse Modify(
            [Description("Asset path of the ScriptableObject (e.g. 'Assets/Data/Weapons/Sword.asset').")] string assetPath,
            [Description("SerializedProperty path (e.g. 'damage', 'stats.health', 'items.Array.data[0]').")] string propertyPath,
            [Description("Value to set. For numbers use the number, for bools use 'true'/'false', " + "for vectors use 'x,y,z', for colors use 'r,g,b,a' (0-1 range), " + "for object references use the asset path.")] string value
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (string.IsNullOrWhiteSpace(propertyPath))
                {
                    return ToolResponse.Error("propertyPath is required.");
                }

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"ScriptableObject not found at '{assetPath}'.");
                }

                var serializedObj = new SerializedObject(asset);
                var prop = serializedObj.FindProperty(propertyPath);

                if (prop == null)
                {
                    return ToolResponse.Error($"Property '{propertyPath}' not found on '{assetPath}'.");
                }

                bool success = SetPropertyValue(prop, value);

                if (!success)
                {
                    return ToolResponse.Error($"Could not set property '{propertyPath}' (type: {prop.propertyType}) to '{value}'.");
                }

                serializedObj.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                return ToolResponse.Text($"Set '{propertyPath}' to '{value}' on '{assetPath}'.");
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Applies a string-encoded value to a <see cref="SerializedProperty"/>.
        /// Supports int, float, bool, string, enum (by index or name), Vector2, Vector3,
        /// Color (comma-separated RGBA floats), and object references (by asset path).
        /// </summary>
        /// <param name="prop">The property to modify.</param>
        /// <param name="value">String-encoded value to apply.</param>
        /// <returns><c>true</c> when the value was successfully parsed and applied; otherwise <c>false</c>.</returns>
        private static bool SetPropertyValue(SerializedProperty prop, string value)
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
                    prop.boolValue = value.ToLowerInvariant() == "true";
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
                    var names = prop.enumNames;
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
                    var parts = value.Split(',');
                    if (parts.Length >= 2 && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    {
                        prop.vector2Value = new Vector2(x, y);
                        return true;
                    }
                    return false;
                }

                case SerializedPropertyType.Vector3:
                {
                    var parts = value.Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) && float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        prop.vector3Value = new Vector3(x, y, z);
                        return true;
                    }
                    return false;
                }

                case SerializedPropertyType.Color:
                {
                    var parts = value.Split(',');
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
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(value);
                    if (obj == null)
                    {
                        return false;
                    }
                    prop.objectReferenceValue = obj;
                    return true;
                }

                default:
                    return false;
            }
        }

        #endregion
    }
}