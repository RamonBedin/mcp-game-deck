#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Reflect
    {
        #region CONSTANTS

        private static readonly string[] _allowedAssemblyPrefixes = new[]
        {
            "UnityEngine",
            "UnityEditor",
            "Unity."
        };

        private static readonly HashSet<string> _blockedTypes = new(StringComparer.Ordinal)
        {
            "UnityEditor.FileUtil",
            "UnityEditor.Build.Pipeline.BuildPipeline",
            "UnityEditor.BuildPipeline",
            "UnityEditor.Compilation.CompilationPipeline",
        };

        private static readonly HashSet<string> _blockedMethods = new(StringComparer.Ordinal)
        {
            "EditorApplication.ExecuteMenuItem",
            "EditorApplication.Exit",
            "EditorApplication.OpenProject",
            "AssetDatabase.DeleteAsset",
            "AssetDatabase.MoveAssetToTrash",
            "EditorPrefs.SetString",
            "EditorPrefs.SetInt",
            "EditorPrefs.SetFloat",
            "EditorPrefs.SetBool",
            "EditorPrefs.DeleteKey",
            "EditorPrefs.DeleteAll",
        };

        #endregion

        #region TOOL METHODS

        /// <summary>
        /// Invokes a public method on a UnityEngine or UnityEditor type via reflection.
        /// For static methods pass <paramref name="instanceId"/> as 0.
        /// For instance methods supply the instance ID of the target GameObject or component.
        /// Arguments are parsed from a JSON array string.
        /// </summary>
        /// <param name="typeName">Fully qualified or simple type name (e.g. 'UnityEngine.Camera', 'Rigidbody').</param>
        /// <param name="methodName">Name of the public method to invoke.</param>
        /// <param name="instanceId">
        /// Instance ID of the target object for instance methods.
        /// Use 0 for static methods.
        /// </param>
        /// <param name="argsJson">
        /// JSON array of arguments to pass to the method (e.g. '["value1", 42, true]').
        /// Leave empty or pass '[]' for methods with no arguments.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the method's return value as text,
        /// or an error when the type/method cannot be found, the type is not from a
        /// UnityEngine/UnityEditor assembly, or the invocation fails.
        /// </returns>
        [McpTool("reflect-call-method", Title = "Reflect / Call Method")]
        [Description("Invokes a public method on a UnityEngine or UnityEditor type via reflection. " + "Safety: only types from UnityEngine/UnityEditor assemblies are allowed. " + "For static methods set instanceId to 0. " + "Pass arguments as a JSON array in argsJson (e.g. '[42, true, \"hello\"]').")]
        public ToolResponse CallMethod(
            [Description("Fully qualified or simple type name (e.g. 'UnityEngine.Camera', 'Rigidbody', 'Physics').")] string typeName,
            [Description("Name of the public method to invoke (e.g. 'Raycast', 'SetActive', 'GetComponent').")] string methodName,
            [Description("Instance ID of the target object for instance methods. Use 0 for static methods.")] int instanceId = 0,
            [Description("JSON array of arguments (e.g. '[42, true, \"hello\"]'). " + "Leave empty or pass '[]' for parameterless methods.")] string argsJson = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return ToolResponse.Error("typeName is required.");
                }

                if (string.IsNullOrWhiteSpace(methodName))
                {
                    return ToolResponse.Error("methodName is required.");
                }

                var type = FindType(typeName);

                if (type == null)
                {
                    return ToolResponse.Error($"Type '{typeName}' not found in loaded assemblies.");
                }

                if (!IsAllowedType(type))
                {
                    return ToolResponse.Error($"Type '{type.FullName}' is not from a UnityEngine or UnityEditor assembly. " + "Only UnityEngine.* and UnityEditor.* types are permitted.");
                }

                if (_blockedTypes.Contains(type.FullName ?? string.Empty))
                {
                    return ToolResponse.Error($"Type '{type.FullName}' is blocked from reflection calls for security reasons.");
                }

                if (_blockedMethods.Contains($"{type.Name}.{methodName}"))
                {
                    return ToolResponse.Error($"Method '{type.Name}.{methodName}' is blocked from reflection calls for security reasons.");
                }

                var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                MethodInfo? method = null;

                for (int i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i].Name == methodName && !candidates[i].IsSpecialName)
                    {
                        method = candidates[i];
                        break;
                    }
                }

                if (method == null)
                {
                    return ToolResponse.Error($"Public method '{methodName}' not found on type '{type.FullName}'.");
                }

                object? instance = null;

                if (!method.IsStatic)
                {
                    if (instanceId == 0)
                    {
                        return ToolResponse.Error($"Method '{methodName}' is an instance method. " + "Provide a non-zero instanceId.");
                    }

                    var unityObj = EditorUtility.EntityIdToObject(instanceId);

                    if (unityObj == null)
                    {
                        return ToolResponse.Error($"No object found for instanceId {instanceId}.");
                    }

                    if (type.IsAssignableFrom(unityObj.GetType()))
                    {
                        instance = unityObj;
                    }
                    else if (unityObj is GameObject go)
                    {
                        var comp = go.GetComponent(type);

                        if (comp == null)
                        {
                            return ToolResponse.Error($"GameObject '{go.name}' does not have a component of type '{type.Name}'.");
                        }

                        instance = comp;
                    }
                    else
                    {
                        return ToolResponse.Error($"Object with instanceId {instanceId} is of type '{unityObj.GetType().Name}', " + $"which is not assignable to '{type.Name}'.");
                    }
                }

                object?[]? args = null;
                var methodParams = method.GetParameters();

                if (methodParams.Length > 0)
                {
                    var parsedArgs = ParseArgsJson(argsJson);

                    if (parsedArgs == null)
                    {
                        return ToolResponse.Error($"Failed to parse argsJson. Expected a JSON array with {methodParams.Length} element(s). " + $"Received: '{argsJson}'");
                    }

                    args = CoerceArguments(parsedArgs, methodParams);

                    if (args == null)
                    {
                        return ToolResponse.Error($"Argument count or type mismatch. Method '{methodName}' expects " + $"{methodParams.Length} parameter(s).");
                    }
                }

                object? result;
                try
                {
                    result = method.Invoke(instance, args);
                }
                catch (TargetInvocationException tie)
                {
                    return ToolResponse.Error($"Method '{methodName}' threw an exception: {tie.InnerException?.Message ?? tie.Message}");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Invocation failed: {ex.Message}");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Called {type.Name}.{methodName}()");

                if (method.ReturnType == typeof(void))
                {
                    sb.AppendLine("Return: (void)");
                }
                else
                {
                    sb.AppendLine($"Return ({method.ReturnType.Name}): {result ?? "null"}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Determines whether <paramref name="type"/> originates from an allowed assembly
        /// (UnityEngine or UnityEditor).
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns><c>true</c> when the type's assembly name starts with an allowed prefix.</returns>
        private static bool IsAllowedType(Type type)
        {
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;

            for (int i = 0; i < _allowedAssemblyPrefixes.Length; i++)
            {
                if (assemblyName.StartsWith(_allowedAssemblyPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Parses a JSON array string into a list of raw string tokens.
        /// Handles basic JSON arrays of strings, numbers, and booleans.
        /// Returns <c>null</c> on parse failure.
        /// </summary>
        /// <param name="json">JSON array string to parse.</param>
        /// <returns>List of raw string tokens, or <c>null</c> on failure.</returns>
        private static List<string>? ParseArgsJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
            {
                return new List<string>();
            }

            string trimmed = json.Trim();

            if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
            {
                return null;
            }

            string inner = trimmed[1..^1].Trim();

            if (string.IsNullOrWhiteSpace(inner))
            {
                return new List<string>();
            }

            var tokens = new List<string>();
            int pos = 0;

            while (pos < inner.Length)
            {
                while (pos < inner.Length && (inner[pos] == ',' || char.IsWhiteSpace(inner[pos])))
                {
                    pos++;
                }

                if (pos >= inner.Length)
                {
                    break;
                }

                if (inner[pos] == '"')
                {
                    pos++;
                    var sb = new StringBuilder();

                    while (pos < inner.Length && inner[pos] != '"')
                    {
                        if (inner[pos] == '\\' && pos + 1 < inner.Length)
                        {
                            pos++;
                            sb.Append(inner[pos]);
                        }
                        else
                        {
                            sb.Append(inner[pos]);
                        }

                        pos++;
                    }

                    if (pos < inner.Length)
                    {
                        pos++;
                    }

                    tokens.Add(sb.ToString());
                }
                else
                {
                    int start = pos;

                    while (pos < inner.Length && inner[pos] != ',' && !char.IsWhiteSpace(inner[pos]))
                    {
                        pos++;
                    }

                    tokens.Add(inner[start..pos].Trim());
                }
            }

            return tokens;
        }

        /// <summary>
        /// Coerces a list of raw string tokens to the expected parameter types of a method.
        /// Returns <c>null</c> when the count does not match or a conversion fails.
        /// </summary>
        /// <param name="tokens">Raw string tokens from JSON parsing.</param>
        /// <param name="parameters">Method parameter descriptors.</param>
        /// <returns>Array of coerced arguments, or <c>null</c> on mismatch.</returns>
        private static object?[]? CoerceArguments(List<string> tokens, ParameterInfo[] parameters)
        {
            if (tokens.Count != parameters.Length)
            {
                return null;
            }

            var result = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                string raw = tokens[i];
                Type paramType = parameters[i].ParameterType;

                if (raw == "null")
                {
                    result[i] = null;
                    continue;
                }
                try
                {
                    if (paramType == typeof(string))
                    {
                        result[i] = raw;
                    }
                    else if (paramType == typeof(bool))
                    {
                        result[i] = raw.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (paramType == typeof(int))
                    {
                        result[i] = int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else if (paramType == typeof(float))
                    {
                        result[i] = float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else if (paramType == typeof(double))
                    {
                        result[i] = double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else if (paramType == typeof(long))
                    {
                        result[i] = long.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else if (paramType.IsEnum)
                    {
                        result[i] = Enum.Parse(paramType, raw, ignoreCase: true);
                    }
                    else
                    {
                        result[i] = Convert.ChangeType(raw, paramType, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[MCP] Argument coercion failed: {ex.Message}");
                    return null;
                }
            }

            return result;
        }

        #endregion
    }
}