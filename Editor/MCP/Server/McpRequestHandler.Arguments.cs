#nullable enable

using System;
using System.Collections.Generic;
using GameDeck.MCP.Models;

namespace GameDeck.MCP.Server
{
    public partial class McpRequestHandler
    {
        #region ARGUMENT BUILDING

        /// <summary>
        /// Constructs the argument array for a reflected method invocation from a JSON
        /// arguments object. Each parameter in <paramref name="parameters"/> is looked up
        /// by name in the JSON; absent optional parameters use their declared default values.
        /// </summary>
        /// <param name="parameters">The ordered parameter metadata list for the method.</param>
        /// <param name="argumentsJson">
        /// The raw JSON object containing the caller-supplied argument values,
        /// e.g. <c>{"name":"Player","layer":0}</c>.
        /// </param>
        /// <returns>
        /// An <see cref="object"/>[] aligned with the parameter order of the target method.
        /// </returns>
        /// <exception cref="McpMethodException">
        /// Thrown when a required parameter is missing from the arguments JSON.
        /// </exception>
        private static object?[] BuildArgumentArray(List<McpParameterInfo> parameters, string argumentsJson)
        {
            if (parameters.Count == 0)
            {
                return Array.Empty<object?>();
            }

            object?[] args = new object?[parameters.Count];

            for (int i = 0; i < parameters.Count; i++)
            {
                McpParameterInfo param = parameters[i];
                string rawValue = ExtractRawValueFromJson(argumentsJson, param.Name);

                if (rawValue == string.Empty)
                {
                    if (param.IsOptional)
                    {
                        args[i] = param.DefaultValue;
                    }
                    else
                    {
                        throw new McpMethodException(McpProtocol.INVALID_PARAMS, $"Required parameter '{param.Name}' is missing.");
                    }
                }
                else
                {
                    args[i] = ConvertArgument(rawValue, param.ParameterType);
                }
            }

            return args;
        }

        #endregion

        #region ARGUMENT CONVERSION

        /// <summary>
        /// Converts a raw JSON value string to the target CLR type.
        /// Handles strings (without surrounding quotes), numbers, booleans, and null.
        /// For complex object types the raw JSON is passed through as a string.
        /// </summary>
        /// <param name="rawValue">
        /// The raw JSON token as extracted from the params object.
        /// For JSON strings this is the content without enclosing quotes.
        /// For numbers and booleans this is the literal token text.
        /// </param>
        /// <param name="targetType">The CLR type to convert the value to.</param>
        /// <returns>
        /// An object of <paramref name="targetType"/>, or <c>null</c> when
        /// <paramref name="rawValue"/> is the literal <c>"null"</c>.
        /// </returns>
        private static object? ConvertArgument(string rawValue, Type targetType)
        {
            Type? underlying = Nullable.GetUnderlyingType(targetType);
            Type resolved    = underlying ?? targetType;

            if (rawValue == McpConstants.JSON_NULL)
            {
                return null;
            }

            if (resolved == typeof(string))
            {
                return rawValue;
            }

            if (resolved == typeof(bool))
            {
                if (rawValue == McpConstants.JSON_TRUE || rawValue == McpConstants.JSON_TRUE_PASCAL)
                {
                    return true;
                }

                if (rawValue == McpConstants.JSON_FALSE || rawValue == McpConstants.JSON_FALSE_PASCAL)
                {
                    return false;
                }

                if (bool.TryParse(rawValue, out bool boolResult))
                {
                    return boolResult;
                }

                return false;
            }

            if (resolved == typeof(int))
            {
                if (int.TryParse(rawValue, out int intResult))
                {
                    return intResult;
                }

                return 0;
            }

            if (resolved == typeof(long))
            {
                if (long.TryParse(rawValue, out long longResult))
                {
                    return longResult;
                }

                return 0L;
            }

            if (resolved == typeof(short))
            {
                if (short.TryParse(rawValue, out short shortResult))
                {
                    return shortResult;
                }

                return (short)0;
            }

            if (resolved == typeof(byte))
            {
                if (byte.TryParse(rawValue, out byte byteResult))
                {
                    return byteResult;
                }

                return (byte)0;
            }

            if (resolved == typeof(float))
            {
                if (float.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatResult))
                {
                    return floatResult;
                }

                return 0f;
            }

            if (resolved == typeof(double))
            {
                if (double.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double doubleResult))
                {
                    return doubleResult;
                }

                return 0.0;
            }

            if (resolved == typeof(decimal))
            {
                if (decimal.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out decimal decResult))
                {
                    return decResult;
                }

                return 0m;
            }

            if (resolved.IsEnum)
            {
                try
                {
                    return Enum.Parse(resolved, rawValue, ignoreCase: true);
                }
                catch
                {
                    if (int.TryParse(rawValue, out int enumInt))
                    {
                        return Enum.ToObject(resolved, enumInt);
                    }

                    return Enum.GetValues(resolved).GetValue(0);
                }
            }

            return rawValue;
        }

        #endregion

        #region JSON VALUE EXTRACTION

        /// <summary>
        /// Extracts the string value (unquoted) of a named field from a JSON object.
        /// Delegates to <see cref="McpProtocol.ExtractStringField"/>.
        /// </summary>
        /// <param name="json">Raw JSON string to search.</param>
        /// <param name="fieldName">The JSON key whose string value to extract.</param>
        /// <returns>The unquoted string value, or an empty string if not found.</returns>
        private static string ExtractStringFromJson(string json, string fieldName)
        {
            return McpProtocol.ExtractStringField(json, fieldName);
        }

        /// <summary>
        /// Extracts the raw JSON object or array value of a named field from a JSON object.
        /// Delegates to <see cref="McpProtocol.ExtractObjectField"/>.
        /// </summary>
        /// <param name="json">Raw JSON string to search.</param>
        /// <param name="fieldName">The JSON key whose object/array value to extract.</param>
        /// <returns>The raw JSON object or array as a string, or an empty string if not found.</returns>
        private static string ExtractObjectFromJson(string json, string fieldName)
        {
            return McpProtocol.ExtractObjectField(json, fieldName);
        }

        /// <summary>
        /// Extracts the raw JSON value token for a named field from a JSON object.
        /// For string values the surrounding quotes are stripped and the content is unescaped.
        /// For all other value types (numbers, booleans, objects, arrays) the raw token is returned.
        /// Returns an empty string when the field is absent.
        /// </summary>
        /// <param name="json">The JSON object to search in.</param>
        /// <param name="fieldName">The field name (without quotes).</param>
        /// <returns>
        /// The value as a plain string (for strings: unquoted and unescaped;
        /// for others: the raw JSON token), or empty string when not found.
        /// </returns>
        private static string ExtractRawValueFromJson(string json, string fieldName)
        {
            string key = "\"" + fieldName + "\":";
            int keyIndex = json.IndexOf(key);

            if (keyIndex < 0)
            {
                return string.Empty;
            }

            int valueStart = keyIndex + key.Length;

            while (valueStart < json.Length && json[valueStart] == ' ')
            {
                valueStart++;
            }

            if (valueStart >= json.Length)
            {
                return string.Empty;
            }

            char first = json[valueStart];

            if (first == '"')
            {
                int closingQuote = McpProtocol.FindClosingQuote(json, valueStart + 1);

                if (closingQuote < 0)
                {
                    return string.Empty;
                }

                return McpProtocol.UnescapeJsonString(json, valueStart + 1, closingQuote);
            }

            if (first == '{' || first == '[')
            {
                return ExtractObjectFromJson(json, fieldName);
            }

            int end = valueStart;

            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']' && json[end] != ' ')
            {
                end++;
            }

            return json[valueStart..end];
        }

        #endregion
    }
}