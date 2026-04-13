#nullable enable
using System;
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
        /// Resolves <paramref name="className"/> across loaded assemblies, then lists every
        /// method whose name contains <paramref name="methodName"/> (empty = all methods).
        /// Each entry is formatted as <c>returnType MethodName(paramType1 paramName, ...)</c>.
        /// </summary>
        /// <param name="className">Fully qualified or simple C# class name (e.g. 'Rigidbody', 'UnityEngine.Physics').</param>
        /// <param name="methodName">Method name filter. Empty string returns all methods. Partial match supported.</param>
        /// <param name="scope">
        /// Binding scope filter: "instance" (only instance), "static" (only static), or "all" (both). Default "all".
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing matching method signatures,
        /// or an error when the type is not found.
        /// </returns>
        [McpTool("reflect-find-method", Title = "Reflect / Find Method", ReadOnlyHint = true)]
        [Description("Searches a C# type for methods matching an optional name filter. " + "Returns full signatures: 'returnType MethodName(paramType paramName, ...)'. " + "scope: 'all' (default), 'instance', or 'static'.")]
        public ToolResponse FindMethod(
            [Description("Fully qualified or simple C# class name (e.g. 'Rigidbody', 'UnityEngine.Physics').")] string className,
            [Description("Method name filter. Empty returns all public methods. Partial match is supported.")] string methodName = "",
            [Description("Binding scope: 'all', 'instance', or 'static'. Default 'all'.")] string scope = "all"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(className))
                {
                    return ToolResponse.Error("className is required.");
                }

                Type? type = FindType(className);

                if (type == null)
                {
                    return ToolResponse.Error($"Type '{className}' not found. Use reflect-get-type or reflect-search to discover the correct name.");
                }

                BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly;
                string scopeNorm = scope.Trim().ToLowerInvariant();

                flags |= scopeNorm switch
                {
                    "instance" => BindingFlags.Instance,
                    "static" => BindingFlags.Static,
                    _ => BindingFlags.Instance | BindingFlags.Static,
                };

                MethodInfo[] methods = type.GetMethods(flags);
                string filterLower = methodName.Trim().ToLowerInvariant();

                var sb = new StringBuilder();
                sb.AppendLine($"Type: {type.FullName}");
                int count = 0;

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo m = methods[i];

                    if (m.IsSpecialName)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(filterLower) && m.Name.ToLowerInvariant().IndexOf(filterLower, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    sb.AppendLine(FormatMethodSignature(m));
                    count++;
                }

                if (count == 0)
                {
                    string filter = string.IsNullOrEmpty(methodName) ? "(no public methods)" : $"No methods matching '{methodName}'";
                    sb.AppendLine(filter);
                }
                else
                {
                    sb.AppendLine($"  ({count} method(s) listed)");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Formats a <see cref="MethodInfo"/> into a human-readable signature string.
        /// </summary>
        /// <param name="m">The method to format.</param>
        /// <returns>A signature string in the form <c>[static] returnType MethodName(paramType paramName, ...)</c>.</returns>
        private static string FormatMethodSignature(MethodInfo m)
        {
            string prefix = m.IsStatic ? "static " : "";
            ParameterInfo[] parms = m.GetParameters();
            var paramSb = new StringBuilder();

            for (int i = 0; i < parms.Length; i++)
            {
                if (i > 0)
                {
                    paramSb.Append(", ");
                }

                paramSb.Append(parms[i].ParameterType.Name);
                paramSb.Append(' ');
                paramSb.Append(parms[i].Name);
            }
            return $"  {prefix}{m.ReturnType.Name} {m.Name}({paramSb})";
        }

        #endregion
    }
}