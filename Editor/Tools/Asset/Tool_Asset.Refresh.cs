#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region REFRESH

        /// <summary>
        /// Refreshes the Asset Database, importing any changed or new files.
        /// </summary>
        /// <param name="forceUpdate">When <c>true</c>, forces reimport of all assets. Default <c>false</c>.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the refresh completed.</returns>
        [McpTool("asset-refresh", Title = "Asset / Refresh")]
        [Description("Refreshes the Asset Database to detect new or changed files on disk.")]
        public ToolResponse Refresh(
            [Description("Force reimport all assets. Default false.")] bool forceUpdate = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (forceUpdate)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    AssetDatabase.Refresh();
                }

                return ToolResponse.Text("Asset Database refreshed." + (forceUpdate ? " (force update)" : ""));
            });
        }

        #endregion
    }
}