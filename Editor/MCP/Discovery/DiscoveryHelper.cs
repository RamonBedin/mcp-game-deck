#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using GameDeck.MCP.Models;

namespace GameDeck.MCP.Discovery
{
    /// <summary>
    /// Shared reflection utilities used by <see cref="ToolDiscovery"/>,
    /// <see cref="PromptDiscovery"/>, and <see cref="ResourceDiscovery"/> to avoid
    /// code duplication across the three discovery classes.
    /// </summary>
    internal static class DiscoveryHelper
    {
        #region CONSTANTS

        private static readonly string[] IGNORED_ASSEMBLY_PREFIXES =
        {
            "System",
            "mscorlib",
            "netstandard",
            "Microsoft",
            "Mono.",
            "Unity.",
            "UnityEngine",
            "UnityEditor",
            "nunit.",
            "Newtonsoft",
            "Bee.",
            "ExCSS",
            "ICSharpCode",
            "SyntaxTree",
            "log4net",
        };

        #endregion

        #region ASSEMBLY FILTERING

        /// <summary>
        /// Returns <c>true</c> when <paramref name="assembly"/> should be scanned for
        /// MCP-decorated types. Assemblies whose names start with any of the
        /// <see cref="IGNORED_ASSEMBLY_PREFIXES"/> are skipped.
        /// </summary>
        /// <param name="assembly">The assembly to evaluate.</param>
        /// <returns><c>true</c> if the assembly may contain MCP types; otherwise <c>false</c>.</returns>
        internal static bool ShouldScanAssembly(Assembly assembly)
        {
            string name = assembly.GetName().Name ?? string.Empty;

            for (int i = 0; i < IGNORED_ASSEMBLY_PREFIXES.Length; i++)
            {
                if (name.StartsWith(IGNORED_ASSEMBLY_PREFIXES[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region TYPE LOADING

        /// <summary>
        /// Safely retrieves all types from <paramref name="assembly"/>, handling
        /// <see cref="ReflectionTypeLoadException"/> for assemblies that cannot fully load.
        /// </summary>
        /// <param name="assembly">The assembly to inspect.</param>
        /// <returns>
        /// An array of <see cref="Type"/> objects. May contain <c>null</c> entries when
        /// individual types failed to load.
        /// </returns>
        internal static Type[] GetTypesSafe(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types ?? Array.Empty<Type>();
            }
        }

        #endregion

        #region ATTRIBUTE HELPERS

        /// <summary>
        /// Returns <c>true</c> when <paramref name="type"/> is decorated with
        /// <typeparamref name="TAttr"/> (non-inherited check).
        /// </summary>
        /// <typeparam name="TAttr">The attribute type to look for.</typeparam>
        /// <param name="type">The type to inspect.</param>
        /// <returns><c>true</c> if the attribute is present; otherwise <c>false</c>.</returns>
        internal static bool HasAttribute<TAttr>(Type type) where TAttr : Attribute
        {
            return type.GetCustomAttributes(typeof(TAttr), inherit: false).Length > 0;
        }

        /// <summary>
        /// Returns the first instance of <typeparamref name="TAttr"/> on
        /// <paramref name="method"/>, or <c>null</c> when none is present.
        /// </summary>
        /// <typeparam name="TAttr">The attribute type to look for.</typeparam>
        /// <param name="method">The method to inspect.</param>
        /// <returns>The attribute instance, or <c>null</c>.</returns>
        internal static TAttr? GetAttribute<TAttr>(MethodInfo method) where TAttr : Attribute
        {
            object[] attrs = method.GetCustomAttributes(typeof(TAttr), inherit: false);
            return attrs.Length > 0 ? (TAttr)attrs[0] : null;
        }

        #endregion

        #region PARAMETER BUILDING

        /// <summary>
        /// Builds the ordered list of <see cref="McpParameterInfo"/> for all parameters
        /// declared on <paramref name="method"/>.
        /// </summary>
        /// <param name="method">The method whose signature is to be reflected.</param>
        /// <returns>
        /// A <see cref="List{T}"/> in parameter-declaration order; empty for parameterless methods.
        /// </returns>
        internal static List<McpParameterInfo> BuildParameterList(MethodInfo method)
        {
            ParameterInfo[] rawParams = method.GetParameters();
            var list = new List<McpParameterInfo>(rawParams.Length);

            for (int i = 0; i < rawParams.Length; i++)
            {
                ParameterInfo p = rawParams[i];

                string description = GetParameterDescription(p);

                var paramInfo = new McpParameterInfo(
                    name:          p.Name ?? string.Empty,
                    description:   description,
                    parameterType: p.ParameterType,
                    isOptional:    p.HasDefaultValue,
                    defaultValue:  p.HasDefaultValue ? p.DefaultValue : null);

                list.Add(paramInfo);
            }

            return list;
        }

        /// <summary>
        /// Reads the <see cref="DescriptionAttribute"/> value from <paramref name="parameter"/>,
        /// or returns an empty string when none is present.
        /// </summary>
        /// <param name="parameter">The reflected parameter to inspect.</param>
        /// <returns>The description string, never <c>null</c>.</returns>
        internal static string GetParameterDescription(ParameterInfo parameter)
        {
            object[] attrs = parameter.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);

            if (attrs.Length == 0)
            {
                return string.Empty;
            }

            DescriptionAttribute descAttr = (DescriptionAttribute)attrs[0];
            return descAttr.Description ?? string.Empty;
        }

        #endregion
    }
}
