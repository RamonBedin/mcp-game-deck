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
    public partial class Tool_ScriptableObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Inspects a ScriptableObject asset and enumerates all its serialized properties
        /// with their current values, property types, and full property paths.
        /// </summary>
        /// <param name="assetPath">Asset path of the ScriptableObject (e.g. <c>Assets/Data/Weapons/Sword.asset</c>).</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing the asset type, name, and all serialized
        /// property paths with their types and current values formatted as human-readable text.
        /// </returns>
        [McpTool("scriptableobject-inspect", Title = "ScriptableObject / Inspect")]
        [Description("Inspects a ScriptableObject asset and lists all its serialized properties " + "with their current values, types, and property paths.")]
        public ToolResponse Inspect(
            [Description("Asset path of the ScriptableObject (e.g. 'Assets/Data/Weapons/Sword.asset').")] string assetPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"ScriptableObject not found at '{assetPath}'.");
                }

                var serializedObj = new SerializedObject(asset);
                var sb = new StringBuilder();
                sb.AppendLine($"ScriptableObject: {asset.GetType().FullName}");
                sb.AppendLine($"Path: {assetPath}");
                sb.AppendLine($"Name: {asset.name}");
                sb.AppendLine();
                sb.AppendLine("Serialized Properties:");

                var iterator = serializedObj.GetIterator();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (iterator.propertyPath == "m_Script")
                    {
                        continue;
                    }

                    sb.AppendLine($"  {iterator.propertyPath} ({iterator.propertyType}): {GetPropertyValueString(iterator)}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Extracts a human-readable string representation of a serialized property's current value.
        /// Handles common property types including primitives, vectors, colors, enums, and object references.
        /// Unrecognised types are represented as <c>&lt;PropertyType&gt;</c>.
        /// </summary>
        /// <param name="prop">The serialized property to read.</param>
        /// <returns>A formatted string representation of the property's current value.</returns>
        private static string GetPropertyValueString(SerializedProperty prop)
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
                _ => $"<{prop.propertyType}>"
            };
        }

        #endregion
    }
}