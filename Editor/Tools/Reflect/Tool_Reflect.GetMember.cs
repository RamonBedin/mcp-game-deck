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
    /// <summary>
    /// MCP tools for runtime C# reflection over UnityEngine and UnityEditor assemblies.
    /// Covers type lookup, member signature inspection, method invocation, and type search.
    /// </summary>
    [McpToolType]
    public partial class Tool_Reflect
    {
        #region Tool Methods

        /// <summary>
        /// Gets detailed signature information for a specific member (method, property, field, or event)
        /// of a C# type, including full parameter types, return type, attributes, and overloads.
        /// </summary>
        /// <param name="className">Fully qualified or simple C# class name (e.g. 'UnityEngine.Physics', 'Rigidbody').</param>
        /// <param name="memberName">Member name to inspect (e.g. 'Raycast', 'position', 'velocity').</param>
        /// <returns>A <see cref="ToolResponse"/> with the member signature details, or an error if not found.</returns>
        [McpTool("reflect-get-member", Title = "Reflect / Get Member")]
        [Description("Gets detailed signature information for a specific member (method, property, field, or event) " + "of a C# type. Returns full parameter types, return type, attributes, and overloads.")]
        public ToolResponse GetMember(
            [Description("Fully qualified or simple C# class name (e.g. 'UnityEngine.Physics', 'Rigidbody').")] string className,
            [Description("Member name to inspect (e.g. 'Raycast', 'position', 'velocity').")] string memberName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(className))
                {
                    return ToolResponse.Error("className is required.");
                }

                if (string.IsNullOrWhiteSpace(memberName))
                {
                    return ToolResponse.Error("memberName is required.");
                }

                var type = FindType(className);

                if (type == null)
                {
                    return ToolResponse.Error($"Type '{className}' not found.");
                }

                var members = type.GetMember(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                if (members.Length == 0)
                {
                    return ToolResponse.Error($"Member '{memberName}' not found on type '{type.FullName}'.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Member: {type.FullName}.{memberName}");
                sb.AppendLine();

                for (int mi = 0; mi < members.Length; mi++)
                {
                    switch (members[mi])
                    {
                        case MethodInfo method:
                            sb.AppendLine($"Method{(method.IsStatic ? " (static)" : "")}:");
                            sb.AppendLine($"  Return: {FormatTypeName(method.ReturnType)}");
                            var parms = method.GetParameters();
                            if (parms.Length > 0)
                            {
                                sb.AppendLine("  Parameters:");
                                for (int pi = 0; pi < parms.Length; pi++)
                                {
                                    var p = parms[pi];
                                    var defaultVal = p.HasDefaultValue ? $" = {p.DefaultValue ?? "null"}" : "";
                                    var modifier = p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : "";
                                    sb.AppendLine($"    {modifier}{FormatTypeName(p.ParameterType)} {p.Name}{defaultVal}");
                                }
                            }
                            if (method.IsGenericMethod)
                            {
                                var genericArgs = method.GetGenericArguments();
                                var genericArgNames = new StringBuilder();
                                for (int i = 0; i < genericArgs.Length; i++)
                                {
                                    if (i > 0)
                                    {
                                        genericArgNames.Append(", ");
                                    }
                                    genericArgNames.Append(genericArgs[i].Name);
                                }
                                sb.AppendLine($"  Generic: <{genericArgNames}>");
                            }
                            var attrs = method.GetCustomAttributes(false);
                            if (attrs.Length > 0)
                            {
                                var attrNames = new StringBuilder();
                                for (int i = 0; i < attrs.Length; i++)
                                {
                                    if (i > 0)
                                    {
                                        attrNames.Append(", ");
                                    }
                                    attrNames.Append(attrs[i].GetType().Name);
                                }
                                sb.AppendLine($"  Attributes: [{attrNames}]");
                            }
                            sb.AppendLine();
                            break;

                        case PropertyInfo prop:
                            var propAccess = prop.CanRead && prop.CanWrite ? "get; set;" : prop.CanRead ? "get;" : "set;";
                            var propStatic = (prop.GetMethod?.IsStatic ?? prop.SetMethod?.IsStatic ?? false) ? " (static)" : "";
                            sb.AppendLine($"Property{propStatic}:");
                            sb.AppendLine($"  Type: {FormatTypeName(prop.PropertyType)}");
                            sb.AppendLine($"  Access: {propAccess}");
                            var indexParams = prop.GetIndexParameters();
                            if (indexParams.Length > 0)
                            {
                                sb.AppendLine("  Indexer Parameters:");
                                for (int pi = 0; pi < indexParams.Length; pi++)
                                {
                                    sb.AppendLine($"    {FormatTypeName(indexParams[pi].ParameterType)} {indexParams[pi].Name}");
                                }
                            }
                            sb.AppendLine();
                            break;

                        case FieldInfo field:
                            sb.AppendLine($"Field{(field.IsStatic ? " (static)" : "")}:");
                            sb.AppendLine($"  Type: {FormatTypeName(field.FieldType)}");
                            sb.AppendLine($"  ReadOnly: {field.IsInitOnly}");
                            sb.AppendLine($"  Literal: {field.IsLiteral}");
                            if (field.IsLiteral && field.DeclaringType?.IsEnum != true)
                            {
                                sb.AppendLine($"  Value: {field.GetRawConstantValue()}");
                            }
                            sb.AppendLine();
                            break;

                        case EventInfo evt:
                            sb.AppendLine("Event:");
                            sb.AppendLine($"  Handler Type: {FormatTypeName(evt.EventHandlerType!)}");
                            sb.AppendLine();
                            break;
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Returns a human-readable type name, expanding generic type arguments recursively
        /// and stripping the by-ref marker from <c>ref</c>/<c>out</c> parameters.
        /// </summary>
        /// <param name="type">The type to format.</param>
        /// <returns>A readable string such as "List&lt;GameObject&gt;" or "Boolean".</returns>
        private static string FormatTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var name = type.Name;
                var idx = name.IndexOf('`');

                if (idx > 0)
                {
                    name = name[..idx];
                }

                var genericTypeArgs = type.GetGenericArguments();
                var argNames = new StringBuilder();

                for (int i = 0; i < genericTypeArgs.Length; i++)
                {
                    if (i > 0)
                    {
                        argNames.Append(", ");
                    }

                    argNames.Append(FormatTypeName(genericTypeArgs[i]));
                }
                return $"{name}<{argNames}>";
            }

            if (type.IsByRef)
            {
                return FormatTypeName(type.GetElementType()!);
            }

            return type.Name;
        }

        #endregion
    }
}