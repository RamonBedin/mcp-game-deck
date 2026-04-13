#nullable enable
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Resources
{
    /// <summary>
    /// MCP Resource that retrieves detailed information about a specific GameObject by name,
    /// including transform data, serialized component properties, layer, tag, and children.
    /// </summary>
    [McpResourceType]
    public class Resource_GameObject
    {
        #region CONSTANTS

        private const string MIME_TEXT_PLAIN = "text/plain";
        private const string PROPERTY_SCRIPT = "m_Script";
        private const string FLOAT_FORMAT = "F4";

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Converts a <see cref="SerializedProperty"/> value to its string representation
        /// based on its <see cref="SerializedPropertyType"/>. Handles integers, floats, booleans,
        /// strings, enums, vectors, colors, and object references. Falls back to the property
        /// type name in angle brackets for unsupported types.
        /// </summary>
        /// <param name="prop">The serialized property to read.</param>
        /// <returns>A human-readable string representation of the property value.</returns>
        private static string GetSerializedValue(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                SerializedPropertyType.Float => prop.floatValue.ToString(FLOAT_FORMAT),
                SerializedPropertyType.Boolean => prop.boolValue.ToString(),
                SerializedPropertyType.String => $"\"{prop.stringValue}\"",
                SerializedPropertyType.Enum => prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                    ? prop.enumNames[prop.enumValueIndex] : prop.enumValueIndex.ToString(),
                SerializedPropertyType.Vector2 => prop.vector2Value.ToString(),
                SerializedPropertyType.Vector3 => prop.vector3Value.ToString(),
                SerializedPropertyType.Color => prop.colorValue.ToString(),
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null
                    ? $"{prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})" : "null",
                _ => $"<{prop.propertyType}>"
            };
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns detailed information about a GameObject found by name in the active scene.
        /// </summary>
        /// <param name="uri">The resource URI requested by the MCP client.</param>
        /// <param name="name">The name of the GameObject to look up.</param>
        /// <returns>An array of resource content entries with the GameObject details as plain text.</returns>
        [McpResource
        (
            Name = "GameObject Details",
            Route = "mcp-game-deck://gameobject/{name}",
            MimeType = "text/plain",
            Description = "Retrieves detailed information about a GameObject by name, including " + "transform, components with their serialized properties, layer, tag, and children."
        )]
        public ResourceResponse[] GetGameObject(string uri, string name)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = GameObject.Find(name);

                if (go == null)
                {
                    return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: $"GameObject '{name}' not found in scene.").MakeArray();
                }

                var sb = new StringBuilder();
                sb.AppendLine($"GameObject: {go.name}");
                sb.AppendLine($"  Active: {go.activeSelf} (ActiveInHierarchy: {go.activeInHierarchy})");
                sb.AppendLine($"  Tag: {go.tag}");
                sb.AppendLine($"  Layer: {LayerMask.LayerToName(go.layer)} ({go.layer})");
                sb.AppendLine($"  Static: {GameObjectUtility.GetStaticEditorFlags(go) != 0}");
                sb.AppendLine($"  Instance ID: {go.GetInstanceID()}");

                var t = go.transform;
                sb.AppendLine();
                sb.AppendLine("Transform:");
                sb.AppendLine($"  Position: {t.position}");
                sb.AppendLine($"  Local Position: {t.localPosition}");
                sb.AppendLine($"  Rotation: {t.rotation.eulerAngles}");
                sb.AppendLine($"  Local Scale: {t.localScale}");

                if (t.parent != null)
                {
                    sb.AppendLine($"  Parent: {t.parent.name}");
                }

                sb.AppendLine($"  Children: {t.childCount}");

                var components = go.GetComponents<Component>();
                sb.AppendLine();
                sb.AppendLine($"Components ({components.Length}):");

                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        sb.AppendLine("  [Missing Script]");
                        continue;
                    }

                    if (comp is Transform)
                    {
                        continue;
                    }

                    sb.AppendLine($"  {comp.GetType().Name}:");
                    var so = new SerializedObject(comp);
                    var iter = so.GetIterator();
                    bool enter = true;

                    while (iter.NextVisible(enter))
                    {
                        enter = false;

                        if (iter.propertyPath == PROPERTY_SCRIPT)
                        {
                            continue;
                        }

                        sb.AppendLine($"    {iter.propertyPath}: {GetSerializedValue(iter)}");
                    }
                }

                if (t.childCount > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Children:");

                    for (int i = 0; i < t.childCount; i++)
                    {
                        var child = t.GetChild(i);
                        sb.AppendLine($"  [{i}] {child.name}{(child.gameObject.activeSelf ? "" : " [INACTIVE]")}");
                    }
                }

                return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: sb.ToString()).MakeArray();
            });
        }

        #endregion
    }
}
