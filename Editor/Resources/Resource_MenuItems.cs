#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Resources
{
    /// <summary>
    /// MCP Resource that lists available Unity Editor menu categories,
    /// optionally filtered by a prefix string.
    /// </summary>
    [McpResourceType]
    public class Resource_MenuItems
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Returns a listing of top-level Unity Editor menu categories and common useful commands.
        /// </summary>
        /// <param name="uri">The resource URI requested by the MCP client.</param>
        /// <param name="prefix">Optional prefix to filter menu categories (e.g. 'File', 'Edit', 'GameObject').</param>
        /// <returns>An array of resource content entries containing the menu item listing as plain text.</returns>
        [McpResource
        (
            Name = "Menu Items",
            Route = "mcp-game-deck://menu-items/{prefix}",
            MimeType = "text/plain",
            Description = "Lists available Unity Editor menu items, optionally filtered by prefix " + "(e.g. 'File', 'Edit', 'GameObject', 'Component', 'Window', 'Assets')."
        )]
        public ResourceResponse[] GetMenuItems(string uri, string prefix)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string[] allMenuPaths = new[]
                {
                    "File", "Edit", "Assets", "GameObject", "Component",
                    "Window", "Tools", "Help"
                };

                string[] menuPaths;

                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    var filtered = new List<string>();

                    foreach (var p in allMenuPaths)
                    {
                        if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            filtered.Add(p);
                        }
                    }

                    menuPaths = filtered.Count > 0 ? filtered.ToArray() : new[] { prefix };
                }
                else
                {
                    menuPaths = allMenuPaths;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Available Menu Items:");
                sb.AppendLine("(Note: Only top-level categories are listed. Use batch-execute-menu to run specific items.)");
                sb.AppendLine();

                foreach (var path in menuPaths)
                {
                    sb.AppendLine($"  {path}/");
                }

                sb.AppendLine();
                sb.AppendLine("Common useful menu commands:");
                sb.AppendLine("  File/Save Project");
                sb.AppendLine("  File/Save");
                sb.AppendLine("  Edit/Undo");
                sb.AppendLine("  Edit/Redo");
                sb.AppendLine("  Edit/Select All");
                sb.AppendLine("  Assets/Refresh");
                sb.AppendLine("  Assets/Open C# Project");
                sb.AppendLine("  GameObject/Create Empty");
                sb.AppendLine("  GameObject/3D Object/Cube");
                sb.AppendLine("  Window/General/Console");
                sb.AppendLine("  Window/General/Inspector");

                return ResourceResponse.CreateText(uri: uri, mimeType: "text/plain", text: sb.ToString()).MakeArray();
            });
        }

        #endregion
    }
}