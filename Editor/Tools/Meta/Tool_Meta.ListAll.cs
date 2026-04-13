#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools that expose meta-information about the tool registry itself.
    /// </summary>
    [McpToolType]
    public partial class Tool_Meta
    {
        #region TOOL METHODS

        /// <summary>
        /// Scans all loaded assemblies for methods decorated with <see cref="McpToolAttribute"/>
        /// and returns a sorted list of every registered tool ID and title.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> listing all discovered tool IDs and titles.</returns>
        [McpTool("tool-list-all", Title = "Meta / List All Tools", ReadOnlyHint = true)]
        [Description("Scans all assemblies for MCP tools and returns a full list of tool IDs and display names. " + "Useful for discovering available tools without prior knowledge of the registry.")]
        public ToolResponse ListAll()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var entries = new List<KeyValuePair<string, string>>();

                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type[] types;
                    try
                    {
                        types = assemblies[a].GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types ?? Array.Empty<Type>();
                    }

                    for (int t = 0; t < types.Length; t++)
                    {
                        Type type = types[t];

                        if (type == null)
                        {
                            continue;
                        }
                        object[] typeAttrs = type.GetCustomAttributes(typeof(McpToolTypeAttribute), inherit: false);

                        if (typeAttrs.Length == 0)
                        {
                            continue;
                        }

                        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

                        for (int m = 0; m < methods.Length; m++)
                        {
                            object[] methodAttrs = methods[m].GetCustomAttributes(typeof(McpToolAttribute), inherit: false);

                            if (methodAttrs.Length == 0)
                            {
                                continue;
                            }

                            McpToolAttribute attr = (McpToolAttribute)methodAttrs[0];

                            if (!attr.Enabled)
                            {
                                continue;
                            }

                            string title = string.IsNullOrWhiteSpace(attr.Title) ? attr.Id : attr.Title;
                            entries.Add(new KeyValuePair<string, string>(attr.Id, title));
                        }
                    }
                }

                entries.Sort(CompareToolEntry);

                var sb = new StringBuilder();
                sb.AppendLine($"Discovered {entries.Count} MCP tool(s):");

                for (int i = 0; i < entries.Count; i++)
                {
                    sb.AppendLine($"  [{entries[i].Key}] {entries[i].Value}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region HELPER METHODS

        /// <summary>
        /// Compares two tool registry entries by their key for alphabetical sorting.
        /// </summary>
        /// <param name="x">First entry.</param>
        /// <param name="y">Second entry.</param>
        /// <returns>Ordinal string comparison result.</returns>
        private static int CompareToolEntry(KeyValuePair<string, string> x, KeyValuePair<string, string> y)
        {
            return string.Compare(x.Key, y.Key, StringComparison.Ordinal);
        }

        #endregion
    }
}