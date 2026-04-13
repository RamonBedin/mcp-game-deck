#nullable enable

using System;

namespace GameDeck.MCP.Models
{
    /// <summary>
    /// Immutable metadata record describing a single parameter of an MCP tool, resource, or prompt method.
    /// Instances are produced by the tool-discovery system and stored inside
    /// <see cref="McpToolInfo"/>, <see cref="McpResourceInfo"/>, and <see cref="McpPromptInfo"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSchemaType"/> provides the JSON Schema primitive type string used when the
    /// MCP server emits the tool's input schema to clients. The mapping follows the JSON Schema
    /// specification: <see href="https://json-schema.org/understanding-json-schema/reference/type"/>.
    /// </remarks>
    public sealed class McpParameterInfo
    {
        #region CONSTRUCTOR

        public McpParameterInfo(string name, string description, Type parameterType, bool isOptional, object? defaultValue)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ParameterType = parameterType ?? throw new ArgumentNullException(nameof(parameterType));
            IsOptional = isOptional;
            DefaultValue = defaultValue;
            JsonSchemaType = MapToJsonSchemaType(parameterType);
        }

        #endregion

        #region CONSTANTS

        /// <summary>JSON Schema type for integer CLR types (int, long, short, byte, etc.).</summary>
        internal const string JSON_SCHEMA_INTEGER = "integer";

        /// <summary>JSON Schema type for floating-point CLR types (float, double, decimal).</summary>
        internal const string JSON_SCHEMA_NUMBER = "number";

        /// <summary>JSON Schema type for boolean CLR type.</summary>
        internal const string JSON_SCHEMA_BOOLEAN = "boolean";

        private const string JSON_SCHEMA_STRING = "string";
        private const string JSON_SCHEMA_ARRAY = "array";
        private const string JSON_SCHEMA_OBJECT = "object";

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the parameter name as declared in the C# method signature, e.g. <c>"filter"</c>.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the human-readable description taken from the <c>[Description]</c> attribute
        /// on the parameter, or an empty string when no attribute is present.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets the CLR <see cref="System.Type"/> of the parameter, e.g. <c>typeof(string)</c>.
        /// </summary>
        public Type ParameterType { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the parameter is optional (has a default value in the
        /// method signature). When <c>true</c>, the JSON Schema for this parameter must not list
        /// it under <c>required</c>.
        /// </summary>
        public bool IsOptional { get; private set; }

        /// <summary>
        /// Gets the compile-time default value for optional parameters, or <c>null</c> for
        /// required parameters or when the default is explicitly <c>null</c>.
        /// </summary>
        public object? DefaultValue { get; private set; }

        /// <summary>
        /// Gets the JSON Schema primitive type string for this parameter's CLR type.
        /// The mapping is performed by <see cref="MapToJsonSchemaType"/>.
        /// </summary>
        /// <list type="table">
        ///   <listheader><term>CLR type</term><description>JSON Schema type</description></listheader>
        ///   <item><term><see cref="int"/>, <see cref="long"/>, <see cref="short"/>, <see cref="byte"/></term><description><c>"integer"</c></description></item>
        ///   <item><term><see cref="float"/>, <see cref="double"/>, <see cref="decimal"/></term><description><c>"number"</c></description></item>
        ///   <item><term><see cref="bool"/></term><description><c>"boolean"</c></description></item>
        ///   <item><term><see cref="string"/>, <see cref="char"/></term><description><c>"string"</c></description></item>
        ///   <item><term>Array, <see cref="System.Collections.IList"/></term><description><c>"array"</c></description></item>
        ///   <item><term>All other types</term><description><c>"object"</c></description></item>
        /// </list>
        public string JsonSchemaType { get; private set; }

        #endregion

        #region JSON SCHEMA MAPPING

        /// <summary>
        /// Maps a CLR <see cref="System.Type"/> to the corresponding JSON Schema primitive type string.
        /// Nullable value types (e.g. <c>int?</c>) are unwrapped before mapping.
        /// </summary>
        /// <param name="type">The CLR type to map. Must not be <c>null</c>.</param>
        /// <returns>
        /// One of <c>"integer"</c>, <c>"number"</c>, <c>"boolean"</c>, <c>"string"</c>,
        /// <c>"array"</c>, or <c>"object"</c>.
        /// </returns>
        public static string MapToJsonSchemaType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            var resolved   = underlying ?? type;

            if (resolved == typeof(int) || resolved == typeof(long) || resolved == typeof(short) || resolved == typeof(byte) || resolved == typeof(uint) || resolved == typeof(ulong) || resolved == typeof(ushort) || resolved == typeof(sbyte))
            {
                return JSON_SCHEMA_INTEGER;
            }

            if (resolved == typeof(float) || resolved == typeof(double) || resolved == typeof(decimal))
            {
                return JSON_SCHEMA_NUMBER;
            }

            if (resolved == typeof(bool))
            {
                return JSON_SCHEMA_BOOLEAN;
            }

            if (resolved == typeof(string) || resolved == typeof(char))
            {
                return JSON_SCHEMA_STRING;
            }

            if (resolved.IsArray || typeof(System.Collections.IList).IsAssignableFrom(resolved))
            {
                return JSON_SCHEMA_ARRAY;
            }

            return JSON_SCHEMA_OBJECT;
        }

        #endregion
    }
}
