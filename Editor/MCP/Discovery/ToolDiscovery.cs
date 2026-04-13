#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.MCP.Discovery
{
    /// <summary>
    /// Scans assemblies loaded in the current <see cref="AppDomain"/> for classes decorated
    /// with <see cref="McpToolTypeAttribute"/> and their methods decorated with
    /// <see cref="McpToolAttribute"/>, building a flat list of <see cref="McpToolInfo"/> records
    /// ready for registration in the MCP tool registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Discovery is intentionally performed once at startup (or on demand) and the result cached
    /// by the caller. This class itself holds no state — every call to <see cref="DiscoverTools"/>
    /// performs a fresh scan.
    /// </para>
    /// <para>
    /// Partial classes: because the CLR merges all partial-class parts into a single
    /// <see cref="Type"/> object before assemblies are loaded, each type appears exactly once
    /// in <see cref="Assembly.GetTypes"/>. No de-duplication logic is required here beyond the
    /// standard attribute check on the consolidated type.
    /// </para>
    /// <para>
    /// Only methods where <see cref="McpToolAttribute.Enabled"/> is <c>true</c> are included.
    /// Disabled tools are silently skipped so they can be toggled without removing their
    /// implementations.
    /// </para>
    /// </remarks>
    public static class ToolDiscovery
    {
        #region PRIVATE METHODS

        /// <summary>
        /// Enumerates all public instance and static methods on <paramref name="type"/> that carry
        /// <see cref="McpToolAttribute"/> and appends a <see cref="McpToolInfo"/> to
        /// <paramref name="results"/> for each enabled one.
        /// </summary>
        /// <param name="type">The declaring class type to inspect.</param>
        /// <param name="results">The accumulator list to populate.</param>
        /// <param name="seenIds">Set of already-discovered tool IDs for duplicate detection.</param>
        private static void ScanTypeForTools(Type type, List<McpToolInfo> results, HashSet<string> seenIds)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            for (int m = 0; m < methods.Length; m++)
            {
                MethodInfo method = methods[m];

                McpToolAttribute? toolAttr = DiscoveryHelper.GetAttribute<McpToolAttribute>(method);
                if (toolAttr == null)
                {
                    continue;
                }

                if (!toolAttr.Enabled)
                {
                    continue;
                }

                if (!seenIds.Add(toolAttr.Id))
                {
                    Debug.LogWarning($"[MCP Discovery] Duplicate tool ID '{toolAttr.Id}' found on " +
                        $"'{type.Name}.{method.Name}' — a tool with this ID was already discovered. " +
                        "Only the first registration will be used.");
                    continue;
                }

                List<McpParameterInfo> parameters = DiscoveryHelper.BuildParameterList(method);

                string title = string.IsNullOrWhiteSpace(toolAttr.Title) ? toolAttr.Id : toolAttr.Title;

                string description = !string.IsNullOrWhiteSpace(toolAttr.Description) ? toolAttr.Description : GetMethodDescription(method);

                var info = new McpToolInfo(
                    id: toolAttr.Id,
                    title: title,
                    description: description,
                    readOnlyHint: toolAttr.ReadOnlyHint,
                    idempotentHint: toolAttr.IdempotentHint,
                    parameters: parameters,
                    declaringType: type,
                    method: method);

                results.Add(info);
            }
        }

        /// <summary>
        /// Reads the <see cref="DescriptionAttribute"/> value from
        /// <paramref name="method"/>, or returns an empty string when none is present.
        /// Used as fallback when <see cref="McpToolAttribute.Description"/> is empty.
        /// </summary>
        /// <param name="method">The reflected method to inspect.</param>
        /// <returns>The description string, never <c>null</c>.</returns>
        private static string GetMethodDescription(MethodInfo method)
        {
            object[] attrs = method.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);

            if (attrs.Length == 0)
            {
                return string.Empty;
            }

            DescriptionAttribute descAttr = (DescriptionAttribute)attrs[0];
            return descAttr.Description ?? string.Empty;
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Scans assemblies loaded in the current <see cref="AppDomain"/> for classes
        /// decorated with <see cref="McpToolTypeAttribute"/> and their public methods decorated
        /// with <see cref="McpToolAttribute"/> where <see cref="McpToolAttribute.Enabled"/> is
        /// <c>true</c>. System and Unity assemblies are skipped for performance.
        /// </summary>
        /// <returns>
        /// A new <see cref="List{T}"/> of <see cref="McpToolInfo"/> in assembly/type/method
        /// declaration order. The list is empty when no decorated types are found.
        /// </returns>
        public static List<McpToolInfo> DiscoverTools()
        {
            var results = new List<McpToolInfo>();
            var seenIds = new HashSet<string>();

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

                    if (!DiscoveryHelper.HasAttribute<McpToolTypeAttribute>(type))
                    {
                        continue;
                    }

                    ScanTypeForTools(type, results, seenIds);
                }
            }

            return results;
        }

        #endregion
    }
}
