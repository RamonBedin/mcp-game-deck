#nullable enable
using System;
using System.Reflection;
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_ProBuilder
    {
        #region PRIVATE REFLECTION HELPERS

        /// <summary>Finds a public static method by name on a type.</summary>
        /// <param name="type">The type to search for the method.</param>
        /// <param name="name">The method name to find.</param>
        /// <returns>The matching <see cref="MethodInfo"/>, or <c>null</c> if not found.</returns>
        private static MethodInfo? FindStaticMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        }

        /// <summary>Parses a simple JSON int array string, e.g. "[0,1,2]".</summary>
        /// <param name="json">JSON string containing an integer array.</param>
        /// <returns>The parsed int array, or <c>null</c> if the input is empty or malformed.</returns>
        private static int[]? ParseIntArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            json = json.Trim();

            if (!json.StartsWith("[") || !json.EndsWith("]"))
            {
                return null;
            }

            json = json[1..^1].Trim();

            if (json.Length == 0)
            {
                return new int[0];
            }

            string[] parts = json.Split(',');
            var result = new System.Collections.Generic.List<int>();

            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int val))
                {
                    result.Add(val);
                }
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        /// <summary>
        /// Resolves a JSON array of target descriptors into GameObjects.
        /// Each entry may have "id" (int) and/or "path" (string).
        /// </summary>
        /// <param name="targetsJson">JSON array of objects with optional "id" and "path" fields (e.g. <c>[{"id":123},{"path":"Player"}]</c>).</param>
        /// <returns>A list of resolved GameObjects. Entries that cannot be found are silently skipped.</returns>
        private static System.Collections.Generic.List<GameObject> ResolveTargets(string targetsJson)
        {
            var result = new System.Collections.Generic.List<GameObject>();

            if (string.IsNullOrWhiteSpace(targetsJson))
            {
                return result;
            }

            targetsJson = targetsJson.Trim();

            if (!targetsJson.StartsWith("[") || !targetsJson.EndsWith("]"))
            {
                return result;
            }

            targetsJson = targetsJson[1..^1].Trim();
            int depth = 0;
            int start = -1;

            for (int i = 0; i < targetsJson.Length; i++)
            {
                char c = targetsJson[i];

                if (c == '{')
                {
                    depth++;

                    if (depth == 1)
                    {
                        start = i;
                    }
                }
                else if (c == '}')
                {
                    depth--;

                    if (depth == 0 && start >= 0)
                    {
                        string entry = targetsJson.Substring(start, i - start + 1);
                        var (id, path) = ParseTargetEntry(entry);
                        var go = Tool_Transform.FindGameObject(id, path);

                        if (go != null)
                        {
                            result.Add(go);
                        }

                        start = -1;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a single JSON object entry to extract "id" (int) and "path" (string) fields.
        /// </summary>
        /// <param name="entry">A raw JSON object string (e.g. <c>{"id":123,"path":"Player"}</c>).</param>
        /// <returns>A tuple of (id, path) parsed from the entry. Defaults to (0, "") for missing fields.</returns>
        private static (int id, string path) ParseTargetEntry(string entry)
        {
            int id = 0;
            string path = "";

            int idIdx = entry.IndexOf("\"id\"");

            if (idIdx >= 0)
            {
                int colon = entry.IndexOf(':', idIdx);

                if (colon >= 0)
                {
                    int numEnd = colon + 1;

                    while (numEnd < entry.Length && (char.IsDigit(entry[numEnd]) || entry[numEnd] == '-' || entry[numEnd] == ' '))
                    {
                        numEnd++;
                    }

                    int.TryParse(entry.Substring(colon + 1, numEnd - colon - 1).Trim(), out id);
                }
            }

            int pathIdx = entry.IndexOf("\"path\"");

            if (pathIdx >= 0)
            {
                int colon = entry.IndexOf(':', pathIdx);

                if (colon >= 0)
                {
                    int q1 = entry.IndexOf('"', colon + 1);

                    if (q1 >= 0)
                    {
                        int q2 = entry.IndexOf('"', q1 + 1);

                        if (q2 > q1)
                        {
                            path = entry.Substring(q1 + 1, q2 - q1 - 1);
                        }
                    }
                }
            }

            return (id, path);
        }

        /// <summary>Converts an IEnumerable of int-like objects to int[].</summary>
        /// <param name="rawIndexes">An enumerable collection of values convertible to <see cref="int"/>.</param>
        /// <returns>An int array of the converted values. Non-convertible items are skipped.</returns>
        private static int[] ToIntArray(object rawIndexes)
        {
            var list = new System.Collections.Generic.List<int>();

            if (rawIndexes is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    try
                    {
                        list.Add(Convert.ToInt32(item));
                    }
                    catch(Exception ex)
                    {
                        Debug.LogWarning($"[ProBuilder] ToIntArray skipped non-convertible value: {ex.Message}");
                    }
                }
            }

            return list.ToArray();
        }

        #endregion

        #region HELPERS

        /// <summary>Checks if ProBuilder is installed.</summary>
        /// <returns><c>true</c> if the ProBuilderMesh type can be resolved from loaded assemblies; otherwise <c>false</c>.</returns>
        private static bool IsProBuilderInstalled()
        {
            return GetPBType("UnityEngine.ProBuilder.ProBuilderMesh") != null;
        }

        /// <summary>Resolves a ProBuilder type by full name.</summary>
        /// <param name="fullName">Fully qualified type name (e.g. "UnityEngine.ProBuilder.ProBuilderMesh").</param>
        /// <returns>The resolved <see cref="Type"/>, or <c>null</c> if not found in any loaded assembly.</returns>
        private static Type? GetPBType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName, false);

                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>Gets the ProBuilderMesh component from a GameObject.</summary>
        /// <param name="go">The GameObject to search for a ProBuilderMesh component.</param>
        /// <returns>The ProBuilderMesh <see cref="Component"/>, or <c>null</c> if not found or ProBuilder is not installed.</returns>
        private static UnityEngine.Component? GetPBMesh(GameObject go)
        {
            var pbType = GetPBType("UnityEngine.ProBuilder.ProBuilderMesh");

            if (pbType == null)
            {
                return null;
            }

            return go.GetComponent(pbType);
        }

        /// <summary>Calls a static method on ShapeGenerator.</summary>
        /// <param name="methodName">Name of the static method to invoke on ShapeGenerator.</param>
        /// <param name="args">Arguments to pass to the method.</param>
        /// <returns>The method's return value, or <c>null</c> if ShapeGenerator or the method could not be resolved.</returns>
        private static object? CallShapeGen(string methodName, params object[] args)
        {
            var sgType = GetPBType("UnityEngine.ProBuilder.ShapeGenerator") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.ShapeGenerator");

            if (sgType == null)
            {
                return null;
            }

            var method = sgType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            return method?.Invoke(null, args);
        }

        /// <summary>
        /// Returns a standard error response indicating that ProBuilder is not installed.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> error directing the user to install com.unity.probuilder.</returns>
        private static ToolResponse NotInstalled()
        {
            return ToolResponse.Error("ProBuilder is not installed. Add 'com.unity.probuilder' via Package Manager.");
        }

        #endregion
    }
}