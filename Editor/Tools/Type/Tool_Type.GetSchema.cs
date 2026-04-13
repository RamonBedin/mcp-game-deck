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
    /// MCP tools for reflecting on C# types and generating JSON-like schemas.
    /// </summary>
    [McpToolType]
    public partial class Tool_Type
    {
        #region TOOL METHODS

        /// <summary>
        /// Resolves a C# type by name across all loaded assemblies and returns a JSON-like schema
        /// describing its public fields and properties, including their declared types and
        /// any <see cref="DescriptionAttribute"/> annotations.
        /// </summary>
        /// <param name="typeName">
        /// Fully qualified or simple type name to resolve (e.g. "UnityEngine.Rigidbody" or "Rigidbody").
        /// </param>
        /// <returns>A <see cref="ToolResponse"/> with a JSON-like schema string, or an error if the type is not found.</returns>
        [McpTool("type-get-json-schema", Title = "Type / Get JSON Schema", ReadOnlyHint = true)]
        [Description("Resolves a C# type by name and returns a JSON-like schema of its public fields and properties, " + "including declared types and description annotations. Searches all loaded assemblies.")]
        public ToolResponse GetSchema(
            [Description("Fully qualified or simple class name to reflect (e.g. 'UnityEngine.Rigidbody' or 'Rigidbody').")] string typeName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return ToolResponse.Error("typeName is required.");
                }

                Type? resolved = ResolveType(typeName);

                if (resolved == null)
                {
                    return ToolResponse.Error($"Type '{typeName}' could not be found in any loaded assembly.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Schema for {resolved.FullName}:");
                sb.AppendLine("{");

                bool first = true;
                FieldInfo[] fields = resolved.GetFields(BindingFlags.Public | BindingFlags.Instance);

                for (int i = 0; i < fields.Length; i++)
                {
                    if (!first)
                    {
                        sb.AppendLine(",");
                    }

                    first = false;

                    FieldInfo field = fields[i];
                    string desc = GetMemberDescription(field);
                    string descStr = string.IsNullOrEmpty(desc) ? "" : $"  // {desc}";
                    sb.Append($"  \"{field.Name}\": \"{MapTypeName(field.FieldType)}\"{descStr}");
                }

                PropertyInfo[] props = resolved.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo prop = props[i];
                    ParameterInfo[] indexParams = prop.GetIndexParameters();

                    if (indexParams.Length > 0)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.AppendLine(",");
                    }

                    first = false;

                    string desc = GetMemberDescription(prop);
                    string descStr = string.IsNullOrEmpty(desc) ? "" : $"  // {desc}";
                    string access = prop.CanRead && prop.CanWrite ? "get;set;" : prop.CanRead ? "get;" : "set;";
                    sb.Append($"  \"{prop.Name}\": \"{MapTypeName(prop.PropertyType)}\"  [{access}]{descStr}");
                }

                if (!first)
                {
                    sb.AppendLine();
                }

                sb.AppendLine("}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Attempts to resolve a type by exact full name first, then by simple name across all assemblies.
        /// </summary>
        /// <param name="typeName">Type name to search for.</param>
        /// <returns>The resolved <see cref="Type"/>, or null if not found.</returns>
        private static Type? ResolveType(string typeName)
        {
            Type? t = Type.GetType(typeName, throwOnError: false, ignoreCase: true);

            if (t != null)
            {
                return t;
            }

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

                for (int i = 0; i < types.Length; i++)
                {
                    Type candidate = types[i];

                    if (candidate == null)
                    {
                        continue;
                    }

                    if (string.Equals(candidate.FullName, typeName, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate.Name,     typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a readable type name, using the simple name for well-known Unity and .NET primitives.
        /// </summary>
        /// <param name="type">The type to map.</param>
        /// <returns>A short, readable type name string.</returns>
        private static string MapTypeName(Type type)
        {
            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(float))
            {
                return "float";
            }

            if (type == typeof(double))
            {
                return "double";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(string))
            {
                return "string";
            }

            if (type == typeof(long))
            {
                return "long";
            }

            if (type == typeof(byte))
            {
                return "byte";
            }

            if (type.IsArray)
            {
                return MapTypeName(type.GetElementType()!) + "[]";
            }

            if (type.IsGenericType)
            {
                Type[] args = type.GetGenericArguments();
                string baseName = type.Name;
                int backtick = baseName.IndexOf('`');

                if (backtick > 0)
                {
                    baseName = baseName[..backtick];
                }

                var argNames = new StringBuilder();

                for (int i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                    {
                        argNames.Append(", ");
                    }

                    argNames.Append(MapTypeName(args[i]));
                }

                return $"{baseName}<{argNames}>";
            }

            return type.Name;
        }

        /// <summary>
        /// Reads a <see cref="DescriptionAttribute"/> from a member, returning empty string when absent.
        /// </summary>
        /// <param name="member">The member to inspect.</param>
        /// <returns>Description text, or empty string.</returns>
        private static string GetMemberDescription(MemberInfo member)
        {
            object[] attrs = member.GetCustomAttributes(typeof(DescriptionAttribute), inherit: true);

            if (attrs.Length == 0)
            {
                return string.Empty;
            }

            return ((DescriptionAttribute)attrs[0]).Description ?? string.Empty;
        }

        #endregion
    }
}