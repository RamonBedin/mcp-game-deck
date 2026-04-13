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
        ///   <item><c>set-active</c> — sets the active state of the target (0=false, 1=true via <paramref name="isActive"/>).</item>
        /// </list>
        /// </summary>
        /// <param name="prefabPath">Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <param name="targetChild">Path relative to the Prefab root to a child Transform (e.g. 'Body/Head'). Empty means the root.</param>
        /// <param name="action">Action to perform: set-position, add-component, remove-component, delete-child, set-active.</param>
        /// <param name="posX">Target X position for set-position. Default 0.</param>
        /// <param name="posY">Target Y position for set-position. Default 0.</param>
        /// <param name="posZ">Target Z position for set-position. Default 0.</param>
        /// <param name="componentType">Component type name for add-component or remove-component (e.g. 'Rigidbody').</param>
        /// <param name="deleteChild">Relative child path to destroy for delete-child action.</param>
        /// <param name="isActive">Set active state for set-active: 1=active, 0=inactive. -1 to skip.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming changes, or an error on failure.</returns>
        [McpTool("prefab-modify-contents", Title = "Prefab / Modify Contents")]
        [Description("Modifies the contents of a Prefab asset without entering Prefab Mode. " + "Actions: set-position, add-component, remove-component, delete-child, set-active. " + "Changes are saved back to disk immediately.")]
        public ToolResponse ModifyContents(
            [Description("Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath,
            [Description("Child path relative to Prefab root (e.g. 'Body/Head'). Empty for root.")] string targetChild = "",
            [Description("Action: set-position, add-component, remove-component, delete-child, set-active.")] string action = "set-position",
            [Description("X position for set-position. Default 0.")] float posX = 0f,
            [Description("Y position for set-position. Default 0.")] float posY = 0f,
            [Description("Z position for set-position. Default 0.")] float posZ = 0f,
            [Description("Component type name for add-component or remove-component (e.g. 'Rigidbody').")] string componentType = "",
            [Description("Relative child path to destroy for delete-child action.")] string deleteChild = "",
            [Description("Active state for set-active: 1=active, 0=inactive, -1=skip. Default -1.")] int isActive = -1
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

                    case "set-active":
                    {
                        if (isActive == -1)
                        {
                            PrefabUtility.UnloadPrefabContents(root);
                            return ToolResponse.Error("isActive must be 0 or 1 for set-active action.");
                        }
                        bool active = isActive != 0;
                        target.gameObject.SetActive(active);
                        sb.AppendLine($"  Set active={active} on '{target.name}'.");
                        break;
                    }

                    default:
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                        return ToolResponse.Error($"Unknown action '{action}'. Valid values: set-position, add-component, " + "remove-component, delete-child, set-active.");
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

        #endregion
    }
}