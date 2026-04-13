#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.MCP.Discovery
{
    /// <summary>
    /// Scans assemblies loaded in the current <see cref="AppDomain"/> for public methods
    /// decorated with <see cref="McpPromptAttribute"/>, building a flat list of
    /// <see cref="McpPromptInfo"/> records ready for registration in the MCP prompt registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike tool and resource discovery, prompt discovery does <em>not</em> require the
    /// declaring class to be marked with a companion type-level attribute. A method decorated
    /// with <see cref="McpPromptAttribute"/> is discovered regardless of the class it belongs to.
    /// This mirrors the behaviour described in <see cref="McpPromptAttribute"/>'s remarks and
    /// allows prompt methods to be co-located inside existing tool or resource classes for
    /// organisational convenience.
    /// </para>
    /// <para>
    /// Only methods where <see cref="McpPromptAttribute.Enabled"/> is <c>true</c> are included.
    /// Disabled prompts are silently skipped.
    /// </para>
    /// <para>
    /// This class holds no state. Every call to <see cref="DiscoverPrompts"/> performs a fresh
    /// scan and the caller is responsible for caching the result.
    /// </para>
    /// </remarks>
    public static class PromptDiscovery
    {
        #region PRIVATE METHODS

        /// <summary>
        /// Enumerates all public instance and static methods on <paramref name="type"/> that carry
        /// <see cref="McpPromptAttribute"/> and appends a <see cref="McpPromptInfo"/> to
        /// <paramref name="results"/> for each enabled one.
        /// </summary>
        /// <param name="type">The declaring class type to inspect.</param>
        /// <param name="results">The accumulator list to populate.</param>
        /// <param name="seenNames">Set of already-discovered prompt names for duplicate detection.</param>
        private static void ScanTypeForPrompts(Type type, List<McpPromptInfo> results, HashSet<string> seenNames)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            for (int m = 0; m < methods.Length; m++)
            {
                MethodInfo method = methods[m];

                McpPromptAttribute? promptAttr = DiscoveryHelper.GetAttribute<McpPromptAttribute>(method);

                if (promptAttr == null)
                {
                    continue;
                }

                if (!promptAttr.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(promptAttr.Name))
                {
                    Debug.LogWarning($"[MCP Discovery] Prompt method '{type.Name}.{method.Name}' " +
                        "has [McpPrompt] but Name is empty — skipping. Set Name to register this prompt.");
                    continue;
                }

                if (!seenNames.Add(promptAttr.Name))
                {
                    Debug.LogWarning($"[MCP Discovery] Duplicate prompt name '{promptAttr.Name}' found on " +
                        $"'{type.Name}.{method.Name}' — a prompt with this name was already discovered. " +
                        "Only the first registration will be used.");
                    continue;
                }

                List<McpParameterInfo> parameters = DiscoveryHelper.BuildParameterList(method);

                var info = new McpPromptInfo(
                    name: promptAttr.Name,
                    description: promptAttr.Description,
                    parameters: parameters,
                    declaringType: type,
                    method: method);

                results.Add(info);
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Scans assemblies loaded in the current <see cref="AppDomain"/> for public methods
        /// decorated with <see cref="McpPromptAttribute"/> where
        /// <see cref="McpPromptAttribute.Enabled"/> is <c>true</c>.
        /// System and Unity assemblies are skipped for performance.
        /// </summary>
        /// <returns>
        /// A new <see cref="List{T}"/> of <see cref="McpPromptInfo"/> in
        /// assembly/type/method declaration order. The list is empty when no decorated methods
        /// are found.
        /// </returns>
        public static List<McpPromptInfo> DiscoverPrompts()
        {
            var results   = new List<McpPromptInfo>();
            var seenNames = new HashSet<string>();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int a = 0; a < assemblies.Length; a++)
            {
                Assembly assembly = assemblies[a];

                if (!DiscoveryHelper.ShouldScanAssembly(assembly))
                {
                    continue;
                }

                Type[] types = DiscoveryHelper.GetTypesSafe(assembly);

                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];

                    if (type == null)
                    {
                        continue;
                    }

                    ScanTypeForPrompts(type, results, seenNames);
                }
            }

            return results;
        }

        #endregion
    }
}
