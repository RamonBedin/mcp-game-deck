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
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Loads a Prefab from <paramref name="prefabPath"/>, optionally traverses to a named
        /// child, applies an <paramref name="action"/>, saves the result back, and unloads
        /// the staging scene.
        /// <para>Supported actions:</para>
        /// <list type="bullet">
        ///   <item><c>set-position</c> — sets the Transform position of the target.</item>
        ///   <item><c>add-component</c> — adds a component by <paramref name="componentType"/>.</item>
        ///   <item><c>remove-component</c> — removes a component by <paramref name="componentType"/>.</item>
        ///   <item><c>delete-child</c> — destroys a child found at <paramref name="deleteChild"/>.</item>
        ///   <item><c>set-active</c> — sets the active state of the target via <paramref name="isActive"/> ("true" / "false" / "").</item>
        ///   <item><c>set-component-field</c> — sets a component's serialized field via SerializedObject. Requires <paramref name="componentType"/>, <paramref name="fieldName"/>, and exactly one of <paramref name="fieldValueString"/> / <paramref name="fieldValueInt"/> / <paramref name="fieldValueFloat"/> / <paramref name="fieldValueBool"/> / <paramref name="fieldValueObject"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="prefabPath">Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <param name="targetChild">Path relative to the Prefab root to a child Transform (e.g. 'Body/Head'). Empty means the root. Ignored for 'delete-child' action.</param>
        /// <param name="action">Action to perform: set-position, add-component, remove-component, delete-child, set-active, set-component-field. Required.</param>
        /// <param name="posX">Target X position for set-position. Default 0.</param>
        /// <param name="posY">Target Y position for set-position. Default 0.</param>
        /// <param name="posZ">Target Z position for set-position. Default 0.</param>
        /// <param name="componentType">Component type name for add-component, remove-component, or set-component-field (e.g. 'Rigidbody').</param>
        /// <param name="deleteChild">Relative child path to destroy. Only used for 'delete-child' action.</param>
        /// <param name="isActive">Active state for set-active. One of "true", "false", or "" (skip). Required for set-active; ignored otherwise.</param>
        /// <param name="fieldName">Serialized property name on the target component (case-sensitive).</param>
        /// <param name="fieldValueString">String value for set-component-field (string, vector, color, quaternion, enum name).</param>
        /// <param name="fieldValueInt">Int value for set-component-field (int field or enum index). Sentinel int.MinValue = not provided.</param>
        /// <param name="fieldValueFloat">Float value for set-component-field. Sentinel float.NegativeInfinity = not provided.</param>
        /// <param name="fieldValueBool">Bool value ("true" / "false" / "") for set-component-field.</param>
        /// <param name="fieldValueObject">Asset path of an Object reference for set-component-field. Empty = not provided.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming changes, or an error on failure.</returns>
        [McpTool("prefab-modify-contents", Title = "Prefab / Modify Contents")]
        [Description("Modifies the contents of a Prefab asset without entering Prefab Mode (headless one-shot edit; auto-saves on success). " + "For multi-step interactive edits across Component/Transform/GameObject tools, use 'prefab-open' / 'prefab-save' instead. " + "Actions and the params each one uses: " + "'set-position' uses posX/posY/posZ; " + "'add-component' uses componentType; " + "'remove-component' uses componentType; " + "'delete-child' uses deleteChild; " + "'set-active' uses isActive; " + "'set-component-field' uses componentType + fieldName + one of (fieldValueString / fieldValueInt / fieldValueFloat / fieldValueBool / fieldValueObject). " + "Changes are saved back to disk immediately.")]
        public ToolResponse ModifyContents(
            [Description("Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath,
            [Description("Child path relative to Prefab root (e.g. 'Body/Head'). Empty for root. Ignored for 'delete-child' action (use 'deleteChild' instead).")] string targetChild = "",
            [Description("Action to perform on the prefab contents. Required. One of: 'set-position', 'add-component', 'remove-component', 'delete-child', 'set-active', 'set-component-field'. Empty returns an error listing the valid values.")] string action = "",
            [Description("X position for set-position. Default 0.")] float posX = 0f,
            [Description("Y position for set-position. Default 0.")] float posY = 0f,
            [Description("Z position for set-position. Default 0.")] float posZ = 0f,
            [Description("Component type name for 'add-component', 'remove-component', or 'set-component-field' (e.g. 'Rigidbody').")] string componentType = "",
            [Description("Relative child path to destroy. Only used for 'delete-child' action.")] string deleteChild = "",
            [Description("Active state for 'set-active' action. One of 'true', 'false', or '' (skip). Default ''. Required for 'set-active'; ignored for all other actions.")] string isActive = "",
            [Description("Field name on the target component for 'set-component-field' (case-sensitive; matches the serialized property name). Required for 'set-component-field'.")] string fieldName = "",
            [Description("String value for 'set-component-field' when the field is a string, Vector2/3/4 (\"x,y[,z[,w]]\"), Color (\"r,g,b[,a]\"), Quaternion (\"x,y,z,w\"), or enum (literal name). Empty for non-string fields.")] string fieldValueString = "",
            [Description("Int value for 'set-component-field' when the field is an int or enum (numeric). Sentinel -2147483648 (int.MinValue) means 'not provided'.")] int fieldValueInt = int.MinValue,
            [Description("Float value for 'set-component-field' when the field is a float. Sentinel float.NegativeInfinity means 'not provided'.")] float fieldValueFloat = float.NegativeInfinity,
            [Description("Bool value for 'set-component-field' when the field is a bool. One of 'true', 'false', or '' (not provided).")] string fieldValueBool = "",
            [Description("Object-reference asset path for 'set-component-field' when the field is a Unity Object reference (e.g. 'Assets/Materials/Red.mat'). Empty means 'not provided'.")] string fieldValueObject = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    return ToolResponse.Error("prefabPath is required.");
                }

                if (!prefabPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("prefabPath must start with 'Assets/' (e.g. 'Assets/Prefabs/Player.prefab').");
                }

                GameObject root;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                }
                catch (System.Exception ex)
                {
                    return ToolResponse.Error($"Failed to load prefab at '{prefabPath}': {ex.Message}");
                }

                if (root == null)
                {
                    return ToolResponse.Error($"Prefab not found at '{prefabPath}'.");
                }

                Transform target = root.transform;

                if (!string.IsNullOrWhiteSpace(targetChild))
                {
                    Transform? found = root.transform.Find(targetChild);

                    if (found == null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                        return ToolResponse.Error($"Child '{targetChild}' not found in prefab '{prefabPath}'.");
                    }

                    target = found;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Modified prefab '{prefabPath}':");

                if (string.IsNullOrWhiteSpace(action))
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    return ToolResponse.Error("'action' is required. Valid values: set-position, add-component, remove-component, delete-child, set-active, set-component-field.");
                }

                string actionNorm = action.Trim().ToLowerInvariant();

                switch (actionNorm)
                {
                    case "set-position":
                    {
                        target.localPosition = new Vector3(posX, posY, posZ);
                        sb.AppendLine($"  set-position on '{target.name}': ({posX}, {posY}, {posZ})");
                        break;
                    }

                    case "add-component":
                    {
                        if (string.IsNullOrWhiteSpace(componentType))
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("componentType is required for add-component.");
                        }
                        System.Type? type = FindTypeByName(componentType);
                        if (type == null)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error($"Component type '{componentType}' not found.");
                        }
                        if (target.gameObject.GetComponent(type) != null)
                        {
                            sb.AppendLine($"  '{componentType}' already present on '{target.name}' — skipped.");
                        }
                        else
                        {
                            target.gameObject.AddComponent(type);
                            sb.AppendLine($"  Added '{componentType}' to '{target.name}'.");
                        }
                        break;
                    }

                    case "remove-component":
                    {
                        if (string.IsNullOrWhiteSpace(componentType))
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("componentType is required for remove-component.");
                        }
                        System.Type? type = FindTypeByName(componentType);
                        if (type == null)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error($"Component type '{componentType}' not found.");
                        }
                        UnityEngine.Component? comp = target.gameObject.GetComponent(type);
                        if (comp == null)
                        {
                            sb.AppendLine($"  '{componentType}' not found on '{target.name}' — skipped.");
                        }
                        else
                        {
                            Object.DestroyImmediate(comp);
                            sb.AppendLine($"  Removed '{componentType}' from '{target.name}'.");
                        }
                        break;
                    }

                    case "delete-child":
                    {
                        if (string.IsNullOrWhiteSpace(deleteChild))
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("deleteChild path is required for the 'delete-child' action.");
                        }
                        Transform? child = root.transform.Find(deleteChild);
                        if (child == null)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error($"Child '{deleteChild}' not found in prefab.");
                        }
                        string childName = child.name;
                        Object.DestroyImmediate(child.gameObject);
                        sb.AppendLine($"  Deleted child '{childName}'.");
                        break;
                    }

                    case "set-component-field":
                    {
                        if (string.IsNullOrWhiteSpace(componentType))
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("componentType is required for 'set-component-field'.");
                        }

                        if (string.IsNullOrWhiteSpace(fieldName))
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("fieldName is required for 'set-component-field'.");
                        }

                        System.Type? type = FindTypeByName(componentType);

                        if (type == null)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error($"Component type '{componentType}' not found.");
                        }

                        UnityEngine.Component? component = target.gameObject.GetComponent(type);

                        if (component == null)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error($"Component '{componentType}' not found on '{target.name}'. Add it first with 'add-component'.");
                        }

                        var so = new SerializedObject(component);
                        SerializedProperty? prop = so.FindProperty(fieldName);

                        if (prop == null)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error($"Field '{fieldName}' not found on '{componentType}'. Use the serialized name (case-sensitive).");
                        }

                        int providedCount = 0;

                        if (!string.IsNullOrEmpty(fieldValueString))
                        {
                            providedCount++;
                        }

                        if (fieldValueInt != int.MinValue)
                        {
                            providedCount++;
                        }

                        if (!float.IsNegativeInfinity(fieldValueFloat))
                        {
                            providedCount++;
                        }

                        if (!string.IsNullOrEmpty(fieldValueBool))
                        {
                            providedCount++;
                        }

                        if (!string.IsNullOrEmpty(fieldValueObject))
                        {
                            providedCount++;
                        }

                        if (providedCount == 0)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("'set-component-field' requires exactly one of: fieldValueString, fieldValueInt, fieldValueFloat, fieldValueBool, fieldValueObject.");
                        }

                        if (providedCount > 1)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("'set-component-field' accepts exactly one value param. Multiple were provided.");
                        }

                        string? setError = ApplyFieldValue(prop, fieldValueString, fieldValueInt, fieldValueFloat, fieldValueBool, fieldValueObject);

                        if (setError != null)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error(setError);
                        }

                        so.ApplyModifiedPropertiesWithoutUndo();
                        sb.AppendLine($"  Set '{componentType}.{fieldName}' on '{target.name}'.");
                        break;
                    }

                    case "set-active":
                    {
                        string norm = isActive.Trim().ToLowerInvariant();

                        if (string.IsNullOrEmpty(norm))
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("isActive is required for the 'set-active' action. Pass 'true' or 'false'.");
                        }

                        bool active;

                        if (norm == "true")
                        {
                            active = true;
                        }
                        else if (norm == "false")
                        {
                            active = false;
                        }
                        else
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error($"isActive must be 'true', 'false', or '' (skip). Got '{isActive}'.");
                        }

                        target.gameObject.SetActive(active);
                        sb.AppendLine($"  Set active={active} on '{target.name}'.");
                        break;
                    }

                    default:
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                        return ToolResponse.Error($"Unknown action '{action}'. Valid values: set-position, add-component, remove-component, delete-child, set-active, set-component-field.");
                    }
                }

                bool saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath) != null;
                PrefabUtility.UnloadPrefabContents(root);

                if (!saved)
                {
                    return ToolResponse.Error($"Failed to save prefab back to '{prefabPath}'.");
                }

                sb.AppendLine($"  Saved to '{prefabPath}'.");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Searches loaded assemblies for a <see cref="System.Type"/> by simple or fully
        /// qualified name. Returns null when no match is found.
        /// </summary>
        /// <param name="typeName">Simple or fully-qualified type name.</param>
        /// <returns>The matching type, or null.</returns>
        private static System.Type? FindTypeByName(string typeName)
        {
            System.Type? direct = System.Type.GetType(typeName);

            if (direct != null)
            {
                return direct;
            }

            System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            for (int a = 0; a < assemblies.Length; a++)
            {
                System.Type? t = assemblies[a].GetType(typeName, false, true);

                if (t != null)
                {
                    return t;
                }
            }

            for (int a = 0; a < assemblies.Length; a++)
            {
                System.Type[]? types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                {
                    continue;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    if (types[t] == null)
                    {
                        continue;
                    }

                    if (string.Equals(types[t].Name, typeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return types[t];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Applies a value to a SerializedProperty using the first non-empty value param.
        /// Supports primitives (int, float, bool, string, Vector2/3/4, Color, Quaternion, enum)
        /// and ObjectReference. Returns an error string when the field type is unsupported or
        /// the supplied value can't be parsed; returns null on success.
        /// </summary>
        /// <param name="prop">The serialized property to assign into.</param>
        /// <param name="fieldValueString">
        /// Raw string value used for <c>String</c> fields and for any field type whose value
        /// is encoded as text (vectors, color, quaternion, enum-by-name). Pass empty when
        /// not applicable.
        /// </param>
        /// <param name="fieldValueInt">
        /// Integer value used for <c>Integer</c> and <c>Enum</c> (by index) fields.
        /// Pass <see cref="int.MinValue"/> as the sentinel meaning "not supplied".
        /// </param>
        /// <param name="fieldValueFloat">
        /// Float value used for <c>Float</c> fields. Pass <see cref="float.NegativeInfinity"/>
        /// as the sentinel meaning "not supplied".
        /// </param>
        /// <param name="fieldValueBool">
        /// String form of a boolean (<c>"true"</c> or <c>"false"</c>, case-insensitive) used
        /// for <c>Boolean</c> fields. Pass empty when not applicable.
        /// </param>
        /// <param name="fieldValueObject">
        /// Asset path used for <c>ObjectReference</c> fields (e.g.
        /// <c>"Assets/Materials/Red.mat"</c>). Pass empty when not applicable.
        /// </param>
        /// <returns>
        /// <c>null</c> on success; otherwise a human-readable error message describing the
        /// unsupported type or the parse failure.
        /// </returns>
        private static string? ApplyFieldValue(SerializedProperty prop, string fieldValueString, int fieldValueInt, float fieldValueFloat, string fieldValueBool, string fieldValueObject)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                {
                    if (fieldValueInt != int.MinValue)
                    {
                        prop.intValue = fieldValueInt;
                        return null;
                    }

                    if (!string.IsNullOrEmpty(fieldValueString) && int.TryParse(fieldValueString, out int parsed))
                    {
                        prop.intValue = parsed;
                        return null;
                    }

                    return $"Field '{prop.name}' is Integer; provide fieldValueInt or a numeric fieldValueString.";
                }

                case SerializedPropertyType.Float:
                {
                    if (!float.IsNegativeInfinity(fieldValueFloat))
                    {
                        prop.floatValue = fieldValueFloat;
                        return null;
                    }

                    return $"Field '{prop.name}' is Float; provide fieldValueFloat.";
                }

                case SerializedPropertyType.Boolean:
                {
                    string norm = fieldValueBool.Trim().ToLowerInvariant();

                    if (norm == "true")
                    {
                        prop.boolValue = true;
                        return null;
                    }

                    if (norm == "false")
                    {
                        prop.boolValue = false;
                        return null;
                    }

                    return $"Field '{prop.name}' is Boolean; provide fieldValueBool as 'true' or 'false'.";
                }

                case SerializedPropertyType.String:
                {
                    prop.stringValue = fieldValueString;
                    return null;
                }

                case SerializedPropertyType.Enum:
                {
                    if (fieldValueInt != int.MinValue)
                    {
                        prop.enumValueIndex = fieldValueInt;
                        return null;
                    }

                    if (!string.IsNullOrEmpty(fieldValueString))
                    {
                        int idx = System.Array.IndexOf(prop.enumNames, fieldValueString);

                        if (idx >= 0)
                        {
                            prop.enumValueIndex = idx;
                            return null;
                        }

                        return $"Enum value '{fieldValueString}' not found on field '{prop.name}'. Valid: [{string.Join(", ", prop.enumNames)}].";
                    }

                    return $"Field '{prop.name}' is Enum; provide fieldValueInt (index) or fieldValueString (literal name).";
                }

                case SerializedPropertyType.Vector2:
                {
                    if (TryParseVector(fieldValueString, 2, out float[] parts))
                    {
                        prop.vector2Value = new Vector2(parts[0], parts[1]);
                        return null;
                    }

                    return $"Field '{prop.name}' is Vector2; provide fieldValueString as 'x,y'.";
                }

                case SerializedPropertyType.Vector3:
                {
                    if (TryParseVector(fieldValueString, 3, out float[] parts))
                    {
                        prop.vector3Value = new Vector3(parts[0], parts[1], parts[2]);
                        return null;
                    }

                    return $"Field '{prop.name}' is Vector3; provide fieldValueString as 'x,y,z'.";
                }

                case SerializedPropertyType.Vector4:
                {
                    if (TryParseVector(fieldValueString, 4, out float[] parts))
                    {
                        prop.vector4Value = new Vector4(parts[0], parts[1], parts[2], parts[3]);
                        return null;
                    }

                    return $"Field '{prop.name}' is Vector4; provide fieldValueString as 'x,y,z,w'.";
                }

                case SerializedPropertyType.Color:
                {
                    if (TryParseVector(fieldValueString, 3, out float[] rgb))
                    {
                        prop.colorValue = new Color(rgb[0], rgb[1], rgb[2], 1f);
                        return null;
                    }

                    if (TryParseVector(fieldValueString, 4, out float[] rgba))
                    {
                        prop.colorValue = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                        return null;
                    }

                    return $"Field '{prop.name}' is Color; provide fieldValueString as 'r,g,b' or 'r,g,b,a'.";
                }

                case SerializedPropertyType.Quaternion:
                {
                    if (TryParseVector(fieldValueString, 4, out float[] parts))
                    {
                        prop.quaternionValue = new Quaternion(parts[0], parts[1], parts[2], parts[3]);
                        return null;
                    }

                    return $"Field '{prop.name}' is Quaternion; provide fieldValueString as 'x,y,z,w'.";
                }

                case SerializedPropertyType.ObjectReference:
                {
                    if (string.IsNullOrEmpty(fieldValueObject))
                    {
                        return $"Field '{prop.name}' is ObjectReference; provide fieldValueObject as an asset path (e.g. 'Assets/Materials/Red.mat').";
                    }

                    Object? assetObj = AssetDatabase.LoadAssetAtPath<Object>(fieldValueObject);

                    if (assetObj == null)
                    {
                        return $"Object reference asset not found at '{fieldValueObject}'.";
                    }

                    prop.objectReferenceValue = assetObj;
                    return null;
                }

                default:
                {
                    return $"Field '{prop.name}' has unsupported type '{prop.propertyType}'. Supported: Integer, Float, Boolean, String, Enum, Vector2/3/4, Color, Quaternion, ObjectReference.";
                }
            }
        }

        /// <summary>
        /// Parses a comma-separated float vector. Returns false when the count doesn't match.
        /// </summary>
        /// <param name="input">Comma-separated float tokens (e.g. <c>"1.0, 2.0, 3.0"</c>).</param>
        /// <param name="expectedCount">Number of components the result must contain.</param>
        /// <param name="parts">
        /// On success, the parsed components in input order; otherwise an empty array.
        /// </param>
        /// <returns>
        /// <c>true</c> when <paramref name="input"/> contains exactly
        /// <paramref name="expectedCount"/> tokens that all parse as invariant-culture
        /// floats; <c>false</c> otherwise.
        /// </returns>
        private static bool TryParseVector(string input, int expectedCount, out float[] parts)
        {
            parts = System.Array.Empty<float>();

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string[] tokens = input.Split(',');

            if (tokens.Length != expectedCount)
            {
                return false;
            }

            var result = new float[expectedCount];

            for (int i = 0; i < expectedCount; i++)
            {
                if (!float.TryParse(tokens[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result[i]))
                {
                    return false;
                }
            }

            parts = result;
            return true;
        }

        #endregion
    }
}