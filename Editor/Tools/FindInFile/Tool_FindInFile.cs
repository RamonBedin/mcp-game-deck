#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tool that performs text or regex searches across project source files.
    /// </summary>
    [McpToolType]
    public partial class Tool_FindInFile
    {
        #region TOOL METHODS

        /// <summary>
        /// Recursively searches files under the specified folder for a text pattern or regular
        /// expression, returning matching lines with surrounding context.
        /// </summary>
        /// <param name="pattern">Plain-text or regex pattern to search for.</param>
        /// <param name="extension">File extension filter (e.g. ".cs"). Empty matches all text files.</param>
        /// <param name="folder">Folder path relative to the project root to search within.</param>
        /// <param name="regex">When true, treats <paramref name="pattern"/> as a regular expression.</param>
        /// <param name="caseSensitive">When true, the search is case-sensitive.</param>
        /// <param name="maxResults">Maximum number of matching lines to return.</param>
        /// <param name="contextLines">Number of lines to show before and after each match.</param>
        /// <returns>Formatted text with file paths, line numbers, and context for each match.</returns>
        [McpTool("find-in-files", Title = "Editor / Find in Files")]
        [Description("Searches for a text pattern or regex in project files. Returns matching file paths " + "and line numbers with context. Useful for finding usages of a class, method, or string.")]
        public ToolResponse FindInFiles(
            [Description("Search pattern (text or regex).")] string pattern,
            [Description("File extension filter (e.g. '.cs', '.shader', '.uxml'). Empty for all text files.")] string extension = ".cs",
            [Description("Search folder relative to project root (e.g. 'Assets/Scripts'). Default 'Assets'.")] string folder = "Assets",
            [Description("Use regex pattern matching. Default false (plain text search).")] bool regex = false,
            [Description("Case sensitive search. Default false.")] bool caseSensitive = false,
            [Description("Maximum number of results. Default 30.")] int maxResults = 30,
            [Description("Number of context lines to show around each match. Default 1.")] int contextLines = 1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    return ToolResponse.Error("pattern is required.");
                }

                var projectPath = Application.dataPath.Replace("/Assets", "");
                var searchPath = Path.GetFullPath(Path.Combine(projectPath, folder));

                if (!searchPath.StartsWith(Path.GetFullPath(projectPath)))
                {
                    return ToolResponse.Error("folder escapes the project directory.");
                }

                if (!Directory.Exists(searchPath))
                {
                    return ToolResponse.Error($"Folder '{folder}' not found.");
                }

                var searchPattern = string.IsNullOrWhiteSpace(extension) ? "*.*" : $"*{extension}";
                var files = Directory.GetFiles(searchPath, searchPattern, SearchOption.AllDirectories);
                Regex? regexObj = null;

                if (regex)
                {
                    try
                    {
                        var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                        regexObj = new Regex(pattern, options);
                    }
                    catch (Exception ex)
                    {
                        return ToolResponse.Error($"Invalid regex: {ex.Message}");
                    }
                }

                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var sb = new StringBuilder();
                int totalMatches = 0;

                foreach (var file in files)
                {
                    if (totalMatches >= maxResults)
                    {
                        break;
                    }

                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(file);
                    }
                    catch
                    {
                        continue;
                    }

                    var relativePath = file.Replace(projectPath + Path.DirectorySeparatorChar, "").Replace(Path.DirectorySeparatorChar, '/');

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (totalMatches >= maxResults)
                        {
                            break;
                        }

                        bool matches = regex ? regexObj!.IsMatch(lines[i]): lines[i].Contains(pattern, comparison);

                        if (!matches)
                        {
                            continue;
                        }

                        totalMatches++;
                        sb.AppendLine($"{relativePath}:{i + 1}");

                        int start = Math.Max(0, i - contextLines);
                        int end = Math.Min(lines.Length - 1, i + contextLines);

                        for (int j = start; j <= end; j++)
                        {
                            var marker = j == i ? ">" : " ";
                            sb.AppendLine($"  {marker} {j + 1}: {lines[j]}");
                        }

                        sb.AppendLine();
                    }
                }

                if (totalMatches == 0)
                {
                    return ToolResponse.Text($"No matches found for '{pattern}' in {folder}/**/{searchPattern}.");
                }

                sb.Insert(0, $"Found {totalMatches} match(es) for '{pattern}':\n\n");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}