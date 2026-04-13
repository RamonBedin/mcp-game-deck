#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating, inspecting, listing, and modifying ScriptableObject assets in the Unity project.
    /// Covers asset creation by type, serialized property inspection, project-wide listing, and property modification.
    /// </summary>
    [McpToolType]
    public partial class Tool_ScriptableObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a ScriptableObject asset of the specified type. The type must be a class
        /// that inherits from <see cref="ScriptableObject"/> and exist in the project's assemblies.
        /// Intermediate folders are created automatically when they do not already exist.
        /// </summary>
        /// <param name="typeName">
        /// Fully qualified ScriptableObject type name (e.g. <c>MyGame.WeaponConfig</c>).
        /// Must inherit from ScriptableObject.
        /// </param>
        /// <param name="folderPath">
        /// Folder path under Assets where the asset will be created (e.g. <c>Assets/Data/Weapons</c>).
        /// </param>
        /// <param name="assetName">
        /// Asset file name without extension (e.g. <c>Sword</c>). The file will be saved as <c>Sword.asset</c>.
        /// </param>
        /// <param name="overwrite">
        /// When <c>true</c>, an existing asset at the same path is overwritten. Default <c>false</c>.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the asset path on success,
        /// or an error message when the type is not found, is not a ScriptableObject subclass,
        /// or the asset already exists and overwrite is disabled.
        /// </returns>
        [McpTool("scriptableobject-create", Title = "ScriptableObject / Create")]
        [Description("Creates a ScriptableObject asset of the specified type. The type must be a class " + "that inherits from ScriptableObject and exists in the project's assemblies.")]
        public ToolResponse Create(
            [Description("Fully qualified ScriptableObject type name (e.g. 'MyGame.WeaponConfig'). " + "Must inherit from ScriptableObject.")] string typeName,
            [Description("Folder path under Assets where the asset will be created (e.g. 'Assets/Data/Weapons').")] string folderPath,
            [Description("Asset file name without extension (e.g. 'Sword'). Will create 'Sword.asset'.")] string assetName,
            [Description("If true, overwrite existing asset at the same path. Default false.")] bool overwrite = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    return ToolResponse.Error("typeName is required.");
                }

                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    return ToolResponse.Error("folderPath is required.");
                }

                if (string.IsNullOrWhiteSpace(assetName))
                {
                    return ToolResponse.Error("assetName is required.");
                }

                if (!folderPath.StartsWith("Assets/") && folderPath != "Assets")
                {
                    return ToolResponse.Error("folderPath must start with 'Assets/'.");
                }

                Type? soType = null;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                for (int a = 0; a < assemblies.Length; a++)
                {
                    soType = assemblies[a].GetType(typeName, false, true);

                    if (soType != null)
                    {
                        break;
                    }
                }

                if (soType == null)
                {
                    return ToolResponse.Error($"Type '{typeName}' not found. Ensure the class exists and compiles.");
                }

                if (!typeof(ScriptableObject).IsAssignableFrom(soType))
                {
                    return ToolResponse.Error($"Type '{typeName}' does not inherit from ScriptableObject.");
                }

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    var parts = folderPath.Replace("\\", "/").Split('/');
                    var current = parts[0];

                    for (int i = 1; i < parts.Length; i++)
                    {
                        var next = current + "/" + parts[i];

                        if (!AssetDatabase.IsValidFolder(next))
                        {
                            AssetDatabase.CreateFolder(current, parts[i]);
                        }

                        current = next;
                    }
                }

                var assetPath = $"{folderPath}/{assetName}.asset";

                if (!overwrite && AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null)
                {
                    return ToolResponse.Error($"Asset already exists at '{assetPath}'. Set overwrite=true to replace.");
                }

                var instance = ScriptableObject.CreateInstance(soType);
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return ToolResponse.Text($"Created ScriptableObject '{typeName}' at '{assetPath}'.");
            });
        }

        #endregion
    }
}