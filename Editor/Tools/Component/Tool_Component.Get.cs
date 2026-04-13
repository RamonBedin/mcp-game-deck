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
        /// Retrieves all visible serialized properties from a Component on the specified GameObject.
        /// Iterates the component's <see cref="SerializedObject"/> and outputs each property's
        /// name, type, and current value. This is a read-only operation.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <param name="componentType">
        /// Simple or fully-qualified type name of the component to inspect
        /// (e.g. "Rigidbody", "BoxCollider", "Light").
        /// UnityEngine namespaces are searched automatically.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each serialized property with its name, type, and value,
        /// or an error if the GameObject or component cannot be located.
        /// </returns>
        [McpTool("component-get", Title = "Component / Get Properties", ReadOnlyHint = true)]
        [Description("Reads all visible serialized properties from a Component and returns name, type, and value " + "for each. Uses SerializedObject to enumerate properties exactly as the Unity Inspector sees them. " + "This is a read-only operation.")]
        public ToolResponse GetComponentProperties(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Simple or fully-qualified component type name to inspect " + "(e.g. 'Rigidbody', 'BoxCollider', 'Light').")] string componentType = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(componentType))
                {
                    return ToolResponse.Error("componentType is required.");
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var resolvedType = ResolveComponentType(componentType);

                if (resolvedType == null)
                {
                    return ToolResponse.Error($"Could not resolve component type '{componentType}'. " + "Ensure the type name is correct and the assembly is loaded.");
                }

                var component = go.GetComponent(resolvedType);

                if (component == null)
                {
                    return ToolResponse.Error($"Component '{resolvedType.FullName}' not found on '{go.name}'.");
                }

                var serializedObj = new SerializedObject(component);
                var sb = new StringBuilder();

                sb.AppendLine($"Component: {resolvedType.FullName}");
                sb.AppendLine($"  GameObject: {go.name} (instanceId: {go.GetInstanceID()})");
                sb.AppendLine($"  Component instanceId: {component.GetInstanceID()}");
                sb.AppendLine("  Properties:");

                var prop = serializedObj.GetIterator();
                bool enterChildren = true;
                int propCount = 0;

                while (prop.NextVisible(enterChildren))
                {
                    if (prop.name == "m_Script")
                    {
                        enterChildren = false;
                        continue;
                    }

                    enterChildren = false;

                    string valueStr = GetSerializedPropertyValueString(prop);
                    sb.AppendLine($"    [{prop.propertyType}] {prop.name} = {valueStr}");
                    propCount++;
                }

                if (propCount == 0)
                {
                    sb.AppendLine("    (no visible serialized properties)");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Returns a human-readable string representation of a <see cref="SerializedProperty"/> value.
        /// Handles common property types; falls back to a generic description for unsupported types.
        /// </summary>
        /// <param name="prop">The serialized property to read.</param>
        /// <returns>A string representation of the property's current value.</returns>
        private static string GetSerializedPropertyValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();

                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();

                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString(
                        "G6",
                        System.Globalization.CultureInfo.InvariantCulture);

                case SerializedPropertyType.String:
                    return $"\"{prop.stringValue}\"";

                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return $"({c.r:G3}, {c.g:G3}, {c.b:G3}, {c.a:G3})";

                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? $"{prop.objectReferenceValue.GetType().Name}:{prop.objectReferenceValue.name}" : "null";

                case SerializedPropertyType.LayerMask:
                    return prop.intValue.ToString();

                case SerializedPropertyType.Enum:
                    if (prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length)
                    {
                        return prop.enumNames[prop.enumValueIndex];
                    }
                    return prop.enumValueIndex.ToString();

                case SerializedPropertyType.Vector2:
                {
                    var v = prop.vector2Value;
                    return $"({v.x:G6}, {v.y:G6})";
                }

                case SerializedPropertyType.Vector3:
                {
                    var v = prop.vector3Value;
                    return $"({v.x:G6}, {v.y:G6}, {v.z:G6})";
                }

                case SerializedPropertyType.Vector4:
                {
                    var v = prop.vector4Value;
                    return $"({v.x:G6}, {v.y:G6}, {v.z:G6}, {v.w:G6})";
                }

                case SerializedPropertyType.Rect:
                {
                    var r = prop.rectValue;
                    return $"(x:{r.x:G6}, y:{r.y:G6}, w:{r.width:G6}, h:{r.height:G6})";
                }

                case SerializedPropertyType.Bounds:
                {
                    var b = prop.boundsValue;
                    return $"center:{b.center} extents:{b.extents}";
                }

                case SerializedPropertyType.Quaternion:
                {
                    var q = prop.quaternionValue;
                    return $"({q.x:G6}, {q.y:G6}, {q.z:G6}, {q.w:G6})";
                }

                case SerializedPropertyType.AnimationCurve:
                    return prop.animationCurveValue != null ? $"AnimationCurve ({prop.animationCurveValue.length} keys)" : "null";

                case SerializedPropertyType.Vector2Int:
                {
                    var v = prop.vector2IntValue;
                    return $"({v.x}, {v.y})";
                }

                case SerializedPropertyType.Vector3Int:
                {
                    var v = prop.vector3IntValue;
                    return $"({v.x}, {v.y}, {v.z})";
                }

                case SerializedPropertyType.RectInt:
                {
                    var r = prop.rectIntValue;
                    return $"(x:{r.x}, y:{r.y}, w:{r.width}, h:{r.height})";
                }

                case SerializedPropertyType.BoundsInt:
                {
                    var b = prop.boundsIntValue;
                    return $"center:{b.center} size:{b.size}";
                }

                case SerializedPropertyType.Hash128:
                    return prop.hash128Value.ToString();

                default:
                    return $"<{prop.propertyType}>";
            }
        }

        #endregion
    }
}