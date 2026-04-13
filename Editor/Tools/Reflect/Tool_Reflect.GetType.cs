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
        /// Gets a summary of a C# type from loaded assemblies via reflection, including base type,
        /// interfaces, constructors, properties, methods, fields, and enum values.
        /// </summary>
        /// <param name="className">Fully qualified or simple C# class name (e.g. 'UnityEngine.Physics', 'Rigidbody').</param>
        /// <returns>A <see cref="ToolResponse"/> with the type summary, or an error if not found.</returns>
        [McpTool("reflect-get-type", Title = "Reflect / Get Type")]
        [Description("Gets a summary of a C# type from loaded assemblies via reflection. " + "Returns base type, interfaces, public methods, properties, fields, and events. " + "Use this to verify a class/struct exists before writing code.")]
        public ToolResponse GetType(
            [Description("Fully qualified or simple C# class name (e.g. 'UnityEngine.Physics', 'Rigidbody', 'Transform').")] string className
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(className))
                {
                    return ToolResponse.Error("className is required.");
                }

                var type = FindType(className);

                if (type == null)
                {
                    return ToolResponse.Error($"Type '{className}' not found. Try the search action to find the correct name.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Type: {type.FullName}");
                sb.AppendLine($"Assembly: {type.Assembly.GetName().Name}");
                sb.AppendLine($"Kind: {(type.IsClass ? "class" : type.IsValueType ? "struct" : type.IsInterface ? "interface" : type.IsEnum ? "enum" : "other")}");

                if (type.BaseType != null)
                {
                    sb.AppendLine($"Base: {type.BaseType.FullName}");
                }

                var interfaces = type.GetInterfaces();

                if (interfaces.Length > 0)
                {
                    var interfaceNames = new StringBuilder();

                    for (int i = 0; i < interfaces.Length; i++)
                    {
                        if (i > 0)
                        {
                            interfaceNames.Append(", ");
                        }

                        interfaceNames.Append(interfaces[i].Name);
                    }

                    sb.AppendLine($"Interfaces: {interfaceNames}");
                }

                sb.AppendLine();
                var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

                if (ctors.Length > 0)
                {
                    sb.AppendLine($"Constructors ({ctors.Length}):");

                    for (int i = 0; i < ctors.Length; i++)
                    {
                        sb.AppendLine($"  {FormatConstructor(ctors[i])}");
                    }

                    sb.AppendLine();
                }

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

                if (props.Length > 0)
                {
                    sb.AppendLine($"Properties ({props.Length}):");

                    for (int i = 0; i < props.Length; i++)
                    {
                        var p = props[i];
                        var access = p.CanRead && p.CanWrite ? "get; set;" : p.CanRead ? "get;" : "set;";
                        var isStatic = (p.GetMethod?.IsStatic ?? p.SetMethod?.IsStatic ?? false) ? "static " : "";
                        sb.AppendLine($"  {isStatic}{p.PropertyType.Name} {p.Name} {{ {access} }}");
                    }

                    sb.AppendLine();
                }

                var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                var methods = new List<MethodInfo>();

                for (int i = 0; i < allMethods.Length; i++)
                {
                    if (!allMethods[i].IsSpecialName)
                    {
                        methods.Add(allMethods[i]);
                    }
                }

                if (methods.Count > 0)
                {
                    sb.AppendLine($"Methods ({methods.Count}):");

                    for (int i = 0; i < methods.Count; i++)
                    {
                        var m = methods[i];
                        var isStatic = m.IsStatic ? "static " : "";
                        var methodParams = m.GetParameters();
                        var parmSb = new StringBuilder();

                        for (int pi = 0; pi < methodParams.Length; pi++)
                        {
                            if (pi > 0)
                            {
                                parmSb.Append(", ");
                            }

                            parmSb.Append($"{methodParams[pi].ParameterType.Name} {methodParams[pi].Name}");
                        }

                        sb.AppendLine($"  {isStatic}{m.ReturnType.Name} {m.Name}({parmSb})");
                    }

                    sb.AppendLine();
                }

                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

                if (fields.Length > 0)
                {
                    sb.AppendLine($"Fields ({fields.Length}):");

                    for (int i = 0; i < fields.Length; i++)
                    {
                        var isStatic = fields[i].IsStatic ? "static " : "";
                        sb.AppendLine($"  {isStatic}{fields[i].FieldType.Name} {fields[i].Name}");
                    }

                    sb.AppendLine();
                }

                if (type.IsEnum)
                {
                    var values = Enum.GetNames(type);
                    sb.AppendLine($"Enum Values ({values.Length}):");

                    for (int i = 0; i < values.Length; i++)
                    {
                        sb.AppendLine($"  {values[i]} = {Convert.ToInt64(Enum.Parse(type, values[i]))}");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Resolves a C# type by name, searching all loaded assemblies.
        /// First tries an exact match, then a case-insensitive simple name match as fallback.
        /// Assemblies that fail to load types are silently skipped.
        /// </summary>
        /// <param name="className">Fully qualified or simple type name (e.g. "UnityEngine.Physics", "Rigidbody").</param>
        /// <returns>The matching <see cref="Type"/>, or <c>null</c> if not found in any loaded assembly.</returns>
        private static Type? FindType(string className)
        {
            var type = Type.GetType(className);

            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int ai = 0; ai < assemblies.Length; ai++)
            {
                type = assemblies[ai].GetType(className, false, true);
                if (type != null) return type;
            }

            for (int ai = 0; ai < assemblies.Length; ai++)
            {
                try
                {
                    var assemblyTypes = assemblies[ai].GetTypes();

                    for (int ti = 0; ti < assemblyTypes.Length; ti++)
                    {
                        if (assemblyTypes[ti].Name.Equals(className, StringComparison.OrdinalIgnoreCase))
                        {
                            return assemblyTypes[ti];
                        }
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    UnityEngine.Debug.LogWarning($"[Tool_Reflect] Skipped assembly '{assemblies[ai].GetName().Name}': {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Formats a constructor signature as a readable string in the form
        /// <c>TypeName(ParamType paramName, ...)</c>.
        /// </summary>
        /// <param name="ctor">The constructor to format.</param>
        /// <returns>A human-readable constructor signature string.</returns>
        private static string FormatConstructor(ConstructorInfo ctor)
        {
            var ctorParams = ctor.GetParameters();
            var parmSb = new StringBuilder();

            for (int i = 0; i < ctorParams.Length; i++)
            {
                if (i > 0)
                {
                    parmSb.Append(", ");
                }

                parmSb.Append($"{ctorParams[i].ParameterType.Name} {ctorParams[i].Name}");
            }
            return $"{ctor.DeclaringType?.Name}({parmSb})";
        }

        #endregion
    }
}