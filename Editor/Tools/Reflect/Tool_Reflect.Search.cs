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
    public partial class Tool_Reflect
    {
        #region TOOL METHODS

        /// <summary>
        /// Searches for C# types across loaded assemblies by name pattern and returns matching
        /// type names with their assembly and kind (class/struct/interface/enum).
        /// </summary>
        /// <param name="query">Search query — partial type name to match (e.g. 'Raycast', 'NavMesh').</param>
        /// <param name="scope">Assembly scope filter: 'unity', 'packages', 'project', or 'all'. Default 'unity'.</param>
        /// <param name="maxResults">Maximum number of results to return. Default 30.</param>
        /// <returns>A <see cref="ToolResponse"/> listing matching types, or a message if none found.</returns>
        [McpTool("reflect-search", Title = "Reflect / Search Types")]
        [Description("Searches for C# types across loaded assemblies by name pattern. " + "Returns matching type names with their assembly and kind (class/struct/interface/enum). " + "Useful for finding the correct fully qualified name of a Unity API type.")]
        public ToolResponse Search(
            [Description("Search query — partial type name to match (e.g. 'Raycast', 'NavMesh', 'BuildTarget').")] string query,
            [Description("Assembly scope filter: 'unity' (UnityEngine/UnityEditor), 'packages' (Unity packages), " + "'project' (Assembly-CSharp), 'all' (everything). Default 'unity'.")] string scope = "unity",
            [Description("Maximum number of results. Default 30.")] int maxResults = 30
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return ToolResponse.Error("query is required.");
                }

                var scopeLower = scope.ToLowerInvariant();
                var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                var sb = new StringBuilder();
                int count = 0;

                for (int ai = 0; ai < allAssemblies.Length; ai++)
                {
                    var assembly = allAssemblies[ai];

                    if (count >= maxResults)
                    {
                        break;
                    }

                    if (!MatchesScope(assembly, scopeLower))
                    {
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        var loaded = ex.Types;
                        var filtered = new List<Type>();

                        for (int ti = 0; ti < loaded.Length; ti++)
                        {
                            if (loaded[ti] != null)
                            {
                                filtered.Add(loaded[ti]!);
                            }
                        }

                        types = filtered.ToArray();
                    }

                    for (int ti = 0; ti < types.Length; ti++)
                    {
                        var type = types[ti];

                        if (count >= maxResults)
                        {
                            break;
                        }

                        if (!type.IsPublic)
                        {
                            continue;
                        }

                        if (!type.Name.Contains(query, StringComparison.OrdinalIgnoreCase) && !(type.FullName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                        {
                            continue;
                        }

                        var kind = type.IsClass ? "class" : type.IsValueType ? (type.IsEnum ? "enum" : "struct") : type.IsInterface ? "interface" : "other";
                        sb.AppendLine($"  {type.FullName} [{kind}] ({assembly.GetName().Name})");
                        count++;
                    }
                }

                if (count == 0)
                {
                    return ToolResponse.Text($"No types found matching '{query}' in scope '{scope}'.");
                }

                return ToolResponse.Text($"Types matching '{query}' ({count} found):\n{sb}");
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Determines whether an assembly falls within the specified search scope.
        /// </summary>
        /// <param name="assembly">The assembly to evaluate.</param>
        /// <param name="scope">
        /// Scope filter: "unity" matches UnityEngine/UnityEditor assemblies,
        /// "packages" matches Unity.* package assemblies,
        /// "project" matches Assembly-CSharp* assemblies,
        /// "all" matches any assembly.
        /// Defaults to "unity" behaviour for unrecognised values.
        /// </param>
        /// <returns><c>true</c> when the assembly name matches the specified scope; otherwise <c>false</c>.</returns>
        private static bool MatchesScope(Assembly assembly, string scope)
        {
            var name = assembly.GetName().Name ?? "";

            return scope switch
            {
                "unity" => name.StartsWith("UnityEngine", StringComparison.Ordinal) || name.StartsWith("UnityEditor", StringComparison.Ordinal),
                "packages" => name.StartsWith("Unity.", StringComparison.Ordinal),
                "project" => name.StartsWith("Assembly-CSharp", StringComparison.Ordinal),
                "all" => true,
                _ => name.StartsWith("UnityEngine", StringComparison.Ordinal) || name.StartsWith("UnityEditor", StringComparison.Ordinal)
            };
        }

        #endregion
    }
}
