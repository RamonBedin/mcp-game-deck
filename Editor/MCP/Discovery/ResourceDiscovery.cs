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
    /// Scans assemblies loaded in the current <see cref="AppDomain"/> for classes decorated
    /// with <see cref="McpResourceTypeAttribute"/> and their methods decorated with
    /// <see cref="McpResourceAttribute"/>, building a flat list of <see cref="McpResourceInfo"/>
    /// records ready for registration in the MCP resource registry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resources are read-only data endpoints — they expose Unity project or scene state to the
    /// AI agent without performing any mutations. Each method's route template (e.g.
    /// <c>"unity://scene/{name}"</c>) is stored verbatim in <see cref="McpResourceInfo.Route"/>;
    /// the server dispatcher is responsible for matching live request URIs against that template.
    /// </para>
    /// <para>
    /// Only methods where <see cref="McpResourceAttribute.Enabled"/> is <c>true</c> are included.
    /// Disabled resources are silently skipped.
    /// </para>
    /// <para>
    /// This class holds no state. Every call to <see cref="DiscoverResources"/> performs a fresh
    /// scan and the caller is responsible for caching the result.
    /// </para>
    /// </remarks>
    public static class ResourceDiscovery
    {
        #region PRIVATE METHODS

        /// <summary>
        /// Enumerates all public instance and static methods on <paramref name="type"/> that carry
        /// <see cref="McpResourceAttribute"/> and appends a <see cref="McpResourceInfo"/> to
        /// <paramref name="results"/> for each enabled one.
        /// </summary>
        /// <param name="type">The declaring class type to inspect.</param>
        /// <param name="results">The accumulator list to populate.</param>
        /// <param name="seenRoutes">Set of already-discovered routes for duplicate detection.</param>
        private static void ScanTypeForResources(Type type, List<McpResourceInfo> results, HashSet<string> seenRoutes)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            for (int m = 0; m < methods.Length; m++)
            {
                MethodInfo method = methods[m];

                McpResourceAttribute? resourceAttr = DiscoveryHelper.GetAttribute<McpResourceAttribute>(method);
                if (resourceAttr == null)
                {
                    continue;
                }

                if (!resourceAttr.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(resourceAttr.Route))
                {
                    Debug.LogWarning($"[MCP Discovery] Resource method '{type.Name}.{method.Name}' " +
                        "has [McpResource] but Route is empty — skipping. Set Route to register this resource.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(resourceAttr.Name) && string.IsNullOrWhiteSpace(method.Name))
                {
                    Debug.LogWarning($"[MCP Discovery] Resource method '{type.Name}.{method.Name}' " +
                        "has [McpResource] but Name is empty — skipping.");
                    continue;
                }

                if (!seenRoutes.Add(resourceAttr.Route))
                {
                    Debug.LogWarning($"[MCP Discovery] Duplicate resource route '{resourceAttr.Route}' found on " +
                        $"'{type.Name}.{method.Name}' — a resource with this route was already discovered. " +
                        "Only the first registration will be used.");
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(resourceAttr.Name) ? method.Name : resourceAttr.Name;

                var info = new McpResourceInfo(
                    name: name,
                    route: resourceAttr.Route,
                    mimeType: resourceAttr.MimeType,
                    description: resourceAttr.Description,
                    declaringType: type,
                    method: method);

                results.Add(info);
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Scans assemblies loaded in the current <see cref="AppDomain"/> for classes
        /// decorated with <see cref="McpResourceTypeAttribute"/> and their public methods
        /// decorated with <see cref="McpResourceAttribute"/> where
        /// <see cref="McpResourceAttribute.Enabled"/> is <c>true</c>.
        /// System and Unity assemblies are skipped for performance.
        /// </summary>
        /// <returns>
        /// A new <see cref="List{T}"/> of <see cref="McpResourceInfo"/> in
        /// assembly/type/method declaration order. The list is empty when no decorated types
        /// are found.
        /// </returns>
        public static List<McpResourceInfo> DiscoverResources()
        {
            var results    = new List<McpResourceInfo>();
            var seenRoutes = new HashSet<string>();

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

                    if (!DiscoveryHelper.HasAttribute<McpResourceTypeAttribute>(type))
                    {
                        continue;
                    }

                    ScanTypeForResources(type, results, seenRoutes);
                }
            }

            return results;
        }

        #endregion
    }
}
