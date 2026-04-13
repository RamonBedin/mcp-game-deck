#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Editor
    {
        #region TOOL METHODS

        /// <summary>
        /// Adds a new Tag to the project's Tag Manager.
        /// </summary>
        /// <param name="tagName">Name of the tag to add.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the tag was added,
        /// a text notice if it already exists, or an error if the TagManager could not be loaded.
        /// </returns>
        [McpTool("editor-add-tag", Title = "Editor / Add Tag")]
        [Description("Adds a new tag to the project's Tag Manager. No-op if tag already exists.")]
        public ToolResponse AddTag(
            [Description("Name of the tag to add.")] string tagName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return ToolResponse.Error("tagName is required.");
                }

                var tagManager = LoadTagManager();

                if (tagManager == null)
                {
                    return ToolResponse.Error("Could not load TagManager asset.");
                }

                var tagsProp = tagManager.FindProperty("tags");

                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                    {
                        return ToolResponse.Text($"Tag '{tagName}' already exists.");
                    }
                }

                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
                tagManager.ApplyModifiedProperties();

                return ToolResponse.Text($"Added tag '{tagName}'.");
            });
        }

        /// <summary>
        /// Adds a new Layer in the first available user layer slot.
        /// </summary>
        /// <param name="layerName">Name of the layer to add.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the layer was added at its slot index,
        /// a text notice if it already exists, or an error if no empty slot is available (8–31)
        /// or the TagManager could not be loaded.
        /// </returns>
        [McpTool("editor-add-layer", Title = "Editor / Add Layer")]
        [Description("Adds a new layer in the first empty user layer slot (8-31). No-op if layer already exists.")]
        public ToolResponse AddLayer(
            [Description("Name of the layer to add.")] string layerName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(layerName))
                {
                    return ToolResponse.Error("layerName is required.");
                }

                var tagManager = LoadTagManager();

                if (tagManager == null)
                {
                    return ToolResponse.Error("Could not load TagManager asset.");
                }

                var layersProp = tagManager.FindProperty("layers");

                for (int i = 0; i < layersProp.arraySize; i++)
                {
                    if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                    {
                        return ToolResponse.Text($"Layer '{layerName}' already exists at index {i}.");
                    }
                }

                for (int i = 8; i < 32 && i < layersProp.arraySize; i++)
                {
                    if (string.IsNullOrEmpty(layersProp.GetArrayElementAtIndex(i).stringValue))
                    {
                        layersProp.GetArrayElementAtIndex(i).stringValue = layerName;
                        tagManager.ApplyModifiedProperties();
                        return ToolResponse.Text($"Added layer '{layerName}' at index {i}.");
                    }
                }

                return ToolResponse.Error("No empty user layer slots available (8-31 are all in use).");
            });
        }

        /// <summary>Removes a tag from the Tag Manager.</summary>
        /// <param name="tagName">Name of the tag to remove.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the tag was removed,
        /// or an error if the tag was not found or the TagManager could not be loaded.
        /// </returns>
        [McpTool("editor-remove-tag", Title = "Editor / Remove Tag")]
        [Description("Removes a user-defined tag from the project's Tag Manager.")]
        public ToolResponse RemoveTag(
            [Description("Name of the tag to remove.")] string tagName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    return ToolResponse.Error("tagName is required.");
                }

                var tagManager = LoadTagManager();

                if (tagManager == null)
                {
                    return ToolResponse.Error("Could not load TagManager asset.");
                }

                var tagsProp = tagManager.FindProperty("tags");

                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                    {
                        tagsProp.DeleteArrayElementAtIndex(i);
                        tagManager.ApplyModifiedProperties();
                        return ToolResponse.Text($"Removed tag '{tagName}'.");
                    }
                }

                return ToolResponse.Error($"Tag '{tagName}' not found.");
            });
        }

        /// <summary>Removes a layer by clearing its slot.</summary>
        /// <param name="layerName">Name of the layer to remove.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the layer slot was cleared,
        /// or an error if the layer was not found in user slots (8–31)
        /// or the TagManager could not be loaded.
        /// </returns>
        [McpTool("editor-remove-layer", Title = "Editor / Remove Layer")]
        [Description("Removes a user-defined layer by clearing its slot in the Tag Manager.")]
        public ToolResponse RemoveLayer(
            [Description("Name of the layer to remove.")] string layerName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(layerName))
                {
                    return ToolResponse.Error("layerName is required.");
                }

                var tagManager = LoadTagManager();

                if (tagManager == null)
                {
                    return ToolResponse.Error("Could not load TagManager asset.");
                }

                var layersProp = tagManager.FindProperty("layers");

                for (int i = 8; i < 32 && i < layersProp.arraySize; i++)
                {
                    if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                    {
                        layersProp.GetArrayElementAtIndex(i).stringValue = "";
                        tagManager.ApplyModifiedProperties();
                        return ToolResponse.Text($"Removed layer '{layerName}' from slot {i}.");
                    }
                }

                return ToolResponse.Error($"Layer '{layerName}' not found in user slots (8-31).");
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Loads the TagManager asset and wraps it in a <see cref="SerializedObject"/>.
        /// Returns null when the asset cannot be found.
        /// </summary>
        /// <returns>A <see cref="SerializedObject"/> wrapping the TagManager, or null.</returns>
        private static SerializedObject? LoadTagManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

            if (assets == null || assets.Length == 0)
            {
                return null;
            }

            return new SerializedObject(assets[0]);
        }

        #endregion
    }
}