#nullable enable
using System;
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
    /// MCP tool that executes multiple Unity Editor operations — either menu commands or API calls —
    /// in sequence, with optional stop-on-error and atomic undo grouping.
    /// </summary>
    [McpToolType]
    public partial class Tool_BatchExecute
    {
        #region CONSTANTS

        private static readonly string[] _blockedMenuPrefixes = new[]
        {
            "File/Build",
            "File/Exit",
            "File/New Project",
            "File/Open Project",
            "File/Open Recent",
        };

        #endregion

        #region Tool Methods

        /// <summary>
        /// Executes multiple Unity Editor menu commands in sequence.
        /// </summary>
        /// <param name="commands">Comma-separated list of menu item paths to execute in order.</param>
        /// <param name="stopOnError">Stop execution on first error. Default true.</param>
        /// <param name="atomic">Group all operations in a single Undo group. Default false.</param>
        /// <returns>A <see cref="ToolResponse"/> summarising how many commands succeeded or failed.</returns>
        [McpTool("batch-execute-menu", Title = "Batch Execute / Menu Commands")]
        [Description("Executes multiple Unity Editor menu commands in sequence. Supports stop-on-error " + "and atomic mode (all changes grouped in a single undo operation). " + "Use this to automate multi-step Editor workflows.")]
        public ToolResponse ExecuteMenuCommands(
            [Description("Comma-separated list of menu item paths to execute in order " + "(e.g. 'File/Save,Edit/Select All,Assets/Refresh').")] string commands,
            [Description("Stop execution on first error. Default true.")] bool stopOnError = true,
            [Description("Group all operations in a single Undo group (atomic). Default false.")] bool atomic = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(commands))
                {
                    return ToolResponse.Error("commands is required. Provide comma-separated menu paths.");
                }

                var menuItems = commands.Split(',');
                var sb = new StringBuilder();
                int succeeded = 0;
                int failed = 0;
                int undoGroup = -1;

                if (atomic)
                {
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Batch Execute");
                }

                foreach (var item in menuItems)
                {
                    var menuPath = item.Trim();

                    if (string.IsNullOrEmpty(menuPath))
                    {
                        continue;
                    }

                    bool isBlocked = false;

                    for (int i = 0; i < _blockedMenuPrefixes.Length; i++)
                    {
                        if (menuPath.StartsWith(_blockedMenuPrefixes[i], StringComparison.OrdinalIgnoreCase))
                        {
                            isBlocked = true;
                            break;
                        }
                    }

                    if (isBlocked)
                    {
                        sb.AppendLine($"  [{succeeded + failed + 1}] {menuPath} — BLOCKED (security)");
                        failed++;

                        if (stopOnError)
                        {
                            if (atomic && undoGroup >= 0)
                            {
                                Undo.RevertAllDownToGroup(undoGroup);
                            }

                            break;
                        }

                        continue;
                    }

                    bool executed = EditorApplication.ExecuteMenuItem(menuPath);

                    if (executed)
                    {
                        sb.AppendLine($"  [{succeeded + failed + 1}] {menuPath} — OK");
                        succeeded++;
                    }
                    else
                    {
                        sb.AppendLine($"  [{succeeded + failed + 1}] {menuPath} — FAILED (menu item not found or disabled)");
                        failed++;

                        if (stopOnError)
                        {
                            if (atomic && undoGroup >= 0)
                                Undo.RevertAllDownToGroup(undoGroup);
                            break;
                        }
                    }
                }

                if (atomic && undoGroup >= 0 && failed == 0)
                {
                    Undo.CollapseUndoOperations(undoGroup);
                }

                sb.Insert(0, $"Batch Execute: {succeeded} succeeded, {failed} failed out of {succeeded + failed}\n");
                return failed > 0 ? ToolResponse.Error(sb.ToString()) : ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>
        /// Executes a sequence of common Unity Editor API operations specified in 'action:arg' format.
        /// </summary>
        /// <param name="operations">Comma-separated operations in 'action:arg' format.</param>
        /// <param name="stopOnError">Stop execution on first error. Default true.</param>
        /// <returns>A <see cref="ToolResponse"/> summarising how many operations succeeded or failed.</returns>
        [McpTool("batch-execute-api", Title = "Batch Execute / API Calls")]
        [Description("Executes a sequence of common Unity Editor API operations. Each operation is specified " + "as 'action:arg' format. Supported actions: select (select asset by path), ping (highlight in Project), " + "refresh (refresh AssetDatabase), save (save all), import (reimport asset by path), " + "delete (delete asset by path), duplicate (duplicate asset by path).")]
        public ToolResponse ExecuteApiCalls(
            [Description("Comma-separated operations in 'action:arg' format " + "(e.g. 'select:Assets/Prefabs/Player.prefab,ping:Assets/Prefabs/Player.prefab,save').")] string operations,
            [Description("Stop execution on first error. Default true.")] bool stopOnError = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(operations))
                {
                    return ToolResponse.Error("operations is required.");
                }

                var ops = operations.Split(',');
                var sb = new StringBuilder();
                int succeeded = 0;
                int failed = 0;

                foreach (var op in ops)
                {
                    var trimmed = op.Trim();

                    if (string.IsNullOrEmpty(trimmed))
                    {
                        continue;
                    }

                    var colonIndex = trimmed.IndexOf(':');
                    var action = colonIndex > 0 ? trimmed[..colonIndex].Trim().ToLowerInvariant() : trimmed.Trim().ToLowerInvariant();
                    var arg = colonIndex > 0 ? trimmed[(colonIndex + 1)..].Trim() : "";
                    try
                    {
                        var result = ExecuteApiCall(action, arg);
                        sb.AppendLine($"  {action}({arg}) — {result}");
                        succeeded++;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  {action}({arg}) — FAILED: {ex.Message}");
                        failed++;

                        if (stopOnError)
                        {
                            break;
                        }
                    }
                }

                sb.Insert(0, $"Batch API: {succeeded} succeeded, {failed} failed\n");
                return failed > 0 ? ToolResponse.Error(sb.ToString()) : ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region HELPER METHODOS

        /// <summary>
        /// Dispatches a single API action (select, ping, refresh, save, import, delete, duplicate)
        /// against the <see cref="AssetDatabase"/> or <see cref="Selection"/>.
        /// </summary>
        /// <param name="action">The action verb to execute (case-sensitive).</param>
        /// <param name="arg">The asset path or argument for the action.</param>
        /// <returns>A human-readable result string describing what was done.</returns>
        /// <exception cref="Exception">Thrown when the asset is not found or the action fails.</exception>
        private static string ExecuteApiCall(string action, string arg)
        {
            bool needsAssetPath = action is "select" or "ping" or "import" or "delete" or "duplicate";

            if (needsAssetPath && !string.IsNullOrEmpty(arg))
            {
                if (!arg.StartsWith("Assets/") && !arg.StartsWith("Packages/"))
                {
                    throw new Exception($"Path must start with 'Assets/' or 'Packages/'. Got: '{arg}'");
                }

                string fullPath = Path.GetFullPath(arg);

                if (!fullPath.StartsWith(Path.GetFullPath("Assets/")) && !fullPath.StartsWith(Path.GetFullPath("Packages/")))
                {
                    throw new Exception("Path escapes allowed directories.");
                }
            }

            switch (action)
            {
                case "select":
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(arg);
                    if (obj == null)
                    {
                        throw new Exception($"Asset not found at '{arg}'.");
                    }
                    Selection.activeObject = obj;
                    return "Selected";

                case "ping":
                    var pingObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(arg);
                    if (pingObj == null)
                    {
                        throw new Exception($"Asset not found at '{arg}'.");
                    }
                    EditorGUIUtility.PingObject(pingObj);
                    return "Pinged";

                case "refresh":
                    AssetDatabase.Refresh();
                    return "AssetDatabase refreshed";

                case "save":
                    AssetDatabase.SaveAssets();
                    return "Assets saved";

                case "import":
                    AssetDatabase.ImportAsset(arg, ImportAssetOptions.ForceUpdate);
                    return $"Reimported '{arg}'";

                case "delete":
                    if (!AssetDatabase.DeleteAsset(arg))
                    {
                        throw new Exception($"Failed to delete '{arg}'.");
                    }
                    return $"Deleted '{arg}'";

                case "duplicate":
                    var newPath = AssetDatabase.GenerateUniqueAssetPath(arg);
                    if (!AssetDatabase.CopyAsset(arg, newPath))
                    {
                        throw new Exception($"Failed to duplicate '{arg}'.");
                    }
                    return $"Duplicated to '{newPath}'";

                default:
                    throw new Exception($"Unknown action '{action}'. Valid: select, ping, refresh, save, import, delete, duplicate.");
            }
        }

        #endregion
    }
}