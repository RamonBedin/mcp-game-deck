#pragma warning disable CS0618
#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for reading and modifying arbitrary Unity objects by instance ID.
    /// </summary>
    [McpToolType]
    public partial class Tool_Object
    {
        #region TOOL METHODS

        /// <summary>
        /// Resolves a Unity object by its instance ID and iterates all visible serialized properties,
        /// returning their paths, types, and current values.
        /// </summary>
        /// <param name="instanceId">Instance ID of the Unity object to inspect.</param>
        /// <returns>A <see cref="ToolResponse"/> with a formatted property listing, or an error message.</returns>
        [McpTool("object-get-data", Title = "Object / Get Data", ReadOnlyHint = true)]
        [Description("Resolves a Unity object by instance ID and returns all visible serialized properties " + "with their paths, types, and current values. Works with any UnityEngine.Object subclass.")]
        public ToolResponse GetData(
            [Description("Instance ID of the Unity object to inspect (e.g. from a GameObject or Component).")] int instanceId
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (instanceId == 0)
                {
                    return ToolResponse.Error("instanceId must not be 0.");
                }

                var obj = EditorUtility.InstanceIDToObject(instanceId);

                if (obj == null)
                {
                    return ToolResponse.Error($"No object found with instanceId {instanceId}.");
                }

                var serializedObj = new SerializedObject(obj);
                var sb = new StringBuilder();
                sb.AppendLine($"Object: {obj.name} ({obj.GetType().Name})");
                sb.AppendLine($"InstanceID: {instanceId}");
                sb.AppendLine("Serialized Properties:");

                SerializedProperty iterator = serializedObj.GetIterator();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    string valueStr = GetPropertyValueString(iterator);
                    sb.AppendLine($"  [{iterator.propertyType}] {iterator.propertyPath} = {valueStr}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Extracts a human-readable string representation of a serialized property's current value.
        /// </summary>
        /// <param name="prop">The property to read.</param>
        /// <returns>String representation of the property value.</returns>
        private static string GetPropertyValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("G");
                case SerializedPropertyType.String:
                    return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Enum:
                {
                    int idx = prop.enumValueIndex;
                    string[] names = prop.enumNames;
                    if (idx >= 0 && idx < names.Length)
                    {
                        return $"{names[idx]} ({idx})";
                    }
                    return idx.ToString();
                }
                case SerializedPropertyType.Vector2:
                    return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect:
                    return prop.rectValue.ToString();
                case SerializedPropertyType.Color:
                    return prop.colorValue.ToString();
                case SerializedPropertyType.Bounds:
                    return prop.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return prop.quaternionValue.ToString();
                case SerializedPropertyType.Vector2Int:
                    return prop.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return prop.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return prop.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return prop.boundsIntValue.ToString();
                case SerializedPropertyType.ObjectReference:
                {
                    var referenced = prop.objectReferenceValue;
                    return referenced == null ? "null" : $"{referenced.name} ({referenced.GetType().Name}, id={referenced.GetInstanceID()})";
                }
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                default:
                    return $"<{prop.propertyType}>";
            }
        }

        #endregion
    }
}