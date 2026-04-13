#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating, reading, updating, validating, and deleting C# script files in the Unity project.
    /// Covers file creation, full content replacement, structured patch edits, compilation validation,
    /// and asset deletion with AssetDatabase integration.
    /// </summary>
    [McpToolType]
    public partial class Tool_Script
    {
        #region TOOL METHODS

        /// <summary>
        /// Reads a script file, applies an ordered list of edit operations, then writes
        /// the result back and triggers an AssetDatabase import.
        /// </summary>
        /// <param name="path">Project-relative or absolute path to the script file.</param>
        /// <param name="editsJson">
        /// JSON array of edit operation objects. Each object must have an <c>"op"</c> field.
        /// Supported operations:
        /// <list type="bullet">
        ///   <item><c>{"op":"replace","old":"text to find","new":"replacement text"}</c> — replaces the first occurrence of <c>old</c> with <c>new</c>.</item>
        ///   <item><c>{"op":"insert_after","anchor":"line to find","text":"text to insert after"}</c> — inserts <c>text</c> on a new line after the first line containing <c>anchor</c>.</item>
        ///   <item><c>{"op":"delete_line","line":5}</c> — deletes the 1-based line number.</item>
        /// </list>
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> summarising each operation that was applied,
        /// or an error when the file is missing or an operation cannot be executed.
        /// </returns>
        [McpTool("script-apply-edits", Title = "Script / Apply Edits")]
        [Description("Applies a sequence of edit operations to a script file and reimports the asset. " + "editsJson is a JSON array with ops: " + "replace ({\"op\":\"replace\",\"old\":\"...\",\"new\":\"...\"}), " + "insert_after ({\"op\":\"insert_after\",\"anchor\":\"...\",\"text\":\"...\"}), " + "delete_line ({\"op\":\"delete_line\",\"line\":5}).")]
        public ToolResponse ApplyEdits(
            [Description("File path to the script (e.g. 'Assets/Scripts/Player.cs').")] string path,
            [Description("JSON array of edit operation objects (replace, insert_after, delete_line).")] string editsJson
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string? pathError = ValidateScriptPath(path);

                if (pathError != null)
                {
                    return ToolResponse.Error(pathError);
                }

                if (string.IsNullOrWhiteSpace(editsJson))
                {
                    return ToolResponse.Error("editsJson is required.");
                }

                if (!File.Exists(path))
                {
                    return ToolResponse.Error($"File not found: '{path}'.");
                }

                string? sizeError = ValidateFileSize(path);

                if (sizeError != null)
                {
                    return ToolResponse.Error(sizeError);
                }

                List<EditOperation> ops = ParseEditOperations(editsJson, out string parseError);

                if (!string.IsNullOrEmpty(parseError))
                {
                    return ToolResponse.Error($"Failed to parse editsJson: {parseError}");
                }

                if (ops.Count == 0)
                {
                    return ToolResponse.Error("editsJson contains no valid operations.");
                }

                string originalContent = File.ReadAllText(path);
                List<string> lines = SplitIntoLines(originalContent);

                var report = new StringBuilder();
                report.AppendLine($"Applied edits to '{path}':");

                for (int opIndex = 0; opIndex < ops.Count; opIndex++)
                {
                    EditOperation op = ops[opIndex];

                    switch (op.Op)
                    {
                        case "replace":
                        {
                            if (string.IsNullOrEmpty(op.Old))
                            {
                                report.AppendLine($"  [SKIP #{opIndex + 1}] replace: 'old' is empty.");
                                break;
                            }
                            string current = JoinLines(lines);
                            int idx = current.IndexOf(op.Old, System.StringComparison.Ordinal);
                            if (idx < 0)
                            {
                                report.AppendLine($"  [SKIP #{opIndex + 1}] replace: text not found: \"{TruncateForLog(op.Old)}\"");
                                break;
                            }
                            string updated = current[..idx] + op.New + current[(idx + op.Old.Length)..];
                            lines = SplitIntoLines(updated);
                            report.AppendLine($"  [OK   #{opIndex + 1}] replace: \"{TruncateForLog(op.Old)}\" → \"{TruncateForLog(op.New)}\"");
                            break;
                        }

                        case "insert_after":
                        {
                            if (string.IsNullOrEmpty(op.Anchor))
                            {
                                report.AppendLine($"  [SKIP #{opIndex + 1}] insert_after: 'anchor' is empty.");
                                break;
                            }
                            int foundLine = -1;
                            for (int li = 0; li < lines.Count; li++)
                            {
                                if (lines[li].Contains(op.Anchor))
                                {
                                    foundLine = li;
                                    break;
                                }
                            }
                            if (foundLine < 0)
                            {
                                report.AppendLine($"  [SKIP #{opIndex + 1}] insert_after: anchor not found: \"{TruncateForLog(op.Anchor)}\"");
                                break;
                            }
                            lines.Insert(foundLine + 1, op.Text ?? string.Empty);
                            report.AppendLine($"  [OK   #{opIndex + 1}] insert_after line {foundLine + 1}: \"{TruncateForLog(op.Anchor)}\"");
                            break;
                        }

                        case "delete_line":
                        {
                            if (op.Line <= 0)
                            {
                                report.AppendLine($"  [SKIP #{opIndex + 1}] delete_line: 'line' must be >= 1.");
                                break;
                            }
                            int zeroIdx = op.Line - 1;
                            if (zeroIdx >= lines.Count)
                            {
                                report.AppendLine($"  [SKIP #{opIndex + 1}] delete_line: line {op.Line} is out of range (file has {lines.Count} lines).");
                                break;
                            }
                            string deletedContent = lines[zeroIdx];
                            lines.RemoveAt(zeroIdx);
                            report.AppendLine($"  [OK   #{opIndex + 1}] delete_line {op.Line}: \"{TruncateForLog(deletedContent)}\"");
                            break;
                        }

                        default:
                            report.AppendLine($"  [SKIP #{opIndex + 1}] Unknown op: '{op.Op}'");
                            break;
                    }
                }

                File.WriteAllText(path, JoinLines(lines));
                AssetDatabase.ImportAsset(path);

                report.AppendLine();
                report.AppendLine("File saved and reimported.");
                return ToolResponse.Text(report.ToString());
            });
        }

        #endregion

        #region EDIT OPERTAION MODEL

        /// <summary>
        /// Represents a single edit instruction parsed from the editsJson array.
        /// </summary>
        private sealed class EditOperation
        {
            #region PROPERTIES

            /// <summary>Gets or sets the operation type: "replace", "insert_after", or "delete_line".</summary>
            public string Op { get; set; } = string.Empty;

            /// <summary>Gets or sets the text to search for in a replace operation.</summary>
            public string Old { get; set; } = string.Empty;

            /// <summary>Gets or sets the replacement text for a replace operation.</summary>
            public string New { get; set; } = string.Empty;

            /// <summary>Gets or sets the anchor text to locate in an insert_after operation.</summary>
            public string Anchor { get; set; } = string.Empty;

            /// <summary>Gets or sets the text to insert in an insert_after operation.</summary>
            public string? Text { get; set; }

            /// <summary>Gets or sets the 1-based line number to delete in a delete_line operation.</summary>
            public int Line { get; set; }

            #endregion
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Parses a JSON array of edit operation objects into a list of <see cref="EditOperation"/>.
        /// No external JSON library is used — this is a minimal hand-written parser.
        /// </summary>
        /// <param name="json">The raw JSON array string.</param>
        /// <param name="error">Set to a non-empty string when parsing fails.</param>
        /// <returns>A list of parsed <see cref="EditOperation"/> instances (may be empty on error).</returns>
        private static List<EditOperation> ParseEditOperations(string json, out string error)
        {
            var result = new List<EditOperation>();
            error = string.Empty;

            string trimmed = json.Trim();

            if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[^1] != ']')
            {
                error = "editsJson must be a JSON array (starting with '[' and ending with ']').";
                return result;
            }

            string inner = trimmed[1..^1].Trim();
            List<string> blocks = ExtractJsonObjects(inner);

            for (int bi = 0; bi < blocks.Count; bi++)
            {
                string block = blocks[bi].Trim();

                if (block.Length < 2)
                {
                    continue;
                }

                var op = new EditOperation
                {
                    Op = ExtractJsonStringField(block, "op"),
                    Old = ExtractJsonStringField(block, "old"),
                    New = ExtractJsonStringField(block, "new"),
                    Anchor = ExtractJsonStringField(block, "anchor"),
                    Text = ExtractJsonStringField(block, "text")
                };

                string lineStr = ExtractJsonStringField(block, "line");

                if (!string.IsNullOrEmpty(lineStr) && int.TryParse(lineStr, out int lineNum))
                {
                    op.Line = lineNum;
                }

                if (!string.IsNullOrWhiteSpace(op.Op))
                {
                    result.Add(op);
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts top-level JSON object blocks from a flat JSON array body.
        /// </summary>
        /// <param name="arrayBody">Content between the outer square brackets of a JSON array.</param>
        /// <returns>A list of raw JSON object strings.</returns>
        private static List<string> ExtractJsonObjects(string arrayBody)
        {
            var blocks = new List<string>();
            int depth = 0;
            bool inString = false;
            int start = -1;

            for (int i = 0; i < arrayBody.Length; i++)
            {
                char c = arrayBody[i];

                if (c == '\\' && inString)
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                }

                else if (c == '}')
                {
                    depth--;

                    if (depth == 0 && start >= 0)
                    {
                        blocks.Add(arrayBody.Substring(start, i - start + 1));
                        start = -1;
                    }
                }
            }

            return blocks;
        }

        /// <summary>
        /// Extracts the string value of a named field from a flat JSON object string.
        /// Handles numeric values by returning them as strings.
        /// Returns an empty string when the field is not found.
        /// </summary>
        /// <param name="jsonObj">A single JSON object string (e.g. <c>{"op":"replace","old":"x"}</c>).</param>
        /// <param name="fieldName">The field name to look up (without quotes).</param>
        /// <returns>The field value as a string, or empty string when absent.</returns>
        private static string ExtractJsonStringField(string jsonObj, string fieldName)
        {
            string key = $"\"{fieldName}\"";
            int keyIdx = jsonObj.IndexOf(key, System.StringComparison.Ordinal);

            if (keyIdx < 0)
            {
                return string.Empty;
            }

            int colonIdx = jsonObj.IndexOf(':', keyIdx + key.Length);

            if (colonIdx < 0)
            {
                return string.Empty;
            }

            int valueStart = colonIdx + 1;

            while (valueStart < jsonObj.Length && jsonObj[valueStart] == ' ')
            {
                valueStart++;
            }

            if (valueStart >= jsonObj.Length)
            {
                return string.Empty;
            }

            if (jsonObj[valueStart] == '"')
            {
                var sb = new StringBuilder();
                int i = valueStart + 1;

                while (i < jsonObj.Length)
                {
                    char c = jsonObj[i];

                    if (c == '\\' && i + 1 < jsonObj.Length)
                    {
                        char escaped = jsonObj[i + 1];

                        switch (escaped)
                        {
                            case '"':
                                sb.Append('"');
                                break;

                            case '\\':
                                sb.Append('\\');
                                break;

                            case 'n':
                                sb.Append('\n');
                                break;

                            case 'r':
                                sb.Append('\r');
                                break;

                            case 't':
                                sb.Append('\t');
                                break;

                            default:
                                sb.Append(escaped);
                                break;
                        }

                        i += 2;
                        continue;
                    }

                    if (c == '"')
                    {
                        break;
                    }
                    sb.Append(c);
                    i++;
                }

                return sb.ToString();
            }
            else
            {
                int end = valueStart;

                while (end < jsonObj.Length && jsonObj[end] != ',' && jsonObj[end] != '}' && jsonObj[end] != ']')
                {
                    end++;
                }

                return jsonObj[valueStart..end].Trim();
            }
        }

        /// <summary>
        /// Splits a string into a mutable list of lines, preserving line content without
        /// terminating newline characters.
        /// </summary>
        /// <param name="content">The full file text.</param>
        /// <returns>A list of line strings.</returns>
        private static List<string> SplitIntoLines(string content)
        {
            var lines = new List<string>();
            int start = 0;

            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    int end = i;

                    if (end > start && content[end - 1] == '\r')
                    {
                        end--;
                    }

                    lines.Add(content[start..end]);
                    start = i + 1;
                }
            }

            if (start < content.Length)
            {
                lines.Add(content[start..]);
            }

            return lines;
        }

        /// <summary>
        /// Joins a list of line strings back into a single string with Unix newlines.
        /// </summary>
        /// <param name="lines">The lines to join.</param>
        /// <returns>The concatenated file content.</returns>
        private static string JoinLines(List<string> lines)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(lines[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a truncated version of a string for use in log/report messages.
        /// </summary>
        /// <param name="text">The string to truncate.</param>
        /// <returns>The string truncated to 60 characters with an ellipsis when needed.</returns>
        private static string TruncateForLog(string? text)
        {
            if (text == null)
            {
                return "(null)";
            }

            if (text.Length <= 60)
            {
                return text;
            }

            return text[..60] + "...";
        }

        /// <summary>
        /// Validates that a script path starts with an allowed prefix and does not
        /// escape the project directory via path traversal (e.g. "Assets/../../etc/passwd").
        /// </summary>
        /// <param name="path">The raw path string from the tool call.</param>
        /// <returns>An error message string if invalid; <c>null</c> if the path is safe.</returns>
        private static string? ValidateScriptPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "path is required.";
            }

            if (!path!.StartsWith("Assets/") && !path.StartsWith("Packages/"))
            {
                return "path must start with 'Assets/' or 'Packages/'.";
            }

            string fullPath = Path.GetFullPath(path);

            if (!fullPath.StartsWith(Path.GetFullPath("Assets/")) && !fullPath.StartsWith(Path.GetFullPath("Packages/")))
            {
                return "Path escapes allowed directories.";
            }

            return null;
        }

        /// <summary>
        /// Checks that a file does not exceed <see cref="GameDeck.MCP.Server.McpConstants.MAX_SCRIPT_FILE_SIZE"/>.
        /// </summary>
        /// <param name="path">Path to the file to check.</param>
        /// <returns>An error message if the file is too large; <c>null</c> if within limits.</returns>
        private static string? ValidateFileSize(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            long size = new FileInfo(path).Length;

            if (size > GameDeck.MCP.Server.McpConstants.MAX_SCRIPT_FILE_SIZE)
            {
                return $"File is too large ({size / (1024 * 1024)}MB). Maximum is 10MB.";
            }

            return null;
        }

        #endregion
    }
}