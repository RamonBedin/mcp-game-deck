#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;

namespace GameDeck.Editor.Prompts
{
    /// <summary>
    /// MCP Prompt that provides a structured workflow for handling GameObjects in Unity —
    /// creating, inspecting, modifying components, and converting to prefabs.
    /// </summary>
    public class Prompt_GameObjectHandling
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Returns the GameObject handling strategy prompt for the given target GameObject name.
        /// </summary>
        /// <param name="gameObjectName">The GameObject name or path to handle.</param>
        /// <returns>A formatted prompt string instructing the AI on the correct workflow.</returns>
        [McpPrompt(Name = "gameobject-handling-strategy")]
        [Description("Workflow for handling GameObjects in Unity — creating, modifying, adding components, and creating prefabs.")]
        public string GameObjectHandlingStrategy([Description("The GameObject name or path to handle.")] string gameObjectName)
        {
            return $@"You are an expert MCP Game Deck with access to MCP tools and resources.

When working with GameObjects in Unity scenes, follow this workflow:

AVAILABLE RESOURCES:
- mcp-game-deck://scenes-hierarchy — list all GameObjects in loaded scenes
- mcp-game-deck://gameobject/{{name}} — get detailed info about a specific GameObject

AVAILABLE TOOLS:
- gameobject-create — create new GameObjects
- update_component — add or modify components on a GameObject
- add-asset-to-scene — instantiate prefabs into the scene
- physics-* tools — configure physics components
- camera-* tools — configure cameras

WORKFLOW for ""{gameObjectName}"":
1. Check mcp-game-deck://scenes-hierarchy to confirm the GameObject exists
2. If it needs to be created, use gameobject-create
3. Use mcp-game-deck://gameobject/{gameObjectName} to inspect current state
4. Use update_component to add/modify components as needed
5. For physics setup, use physics tools (rigidbody, colliders, materials)
6. Confirm success and report what was done

GUIDELINES:
- Always check the hierarchy first before creating duplicates
- Use Undo-compatible operations when possible
- Validate that referenced assets exist before using them
- For prefab workflows, create the GameObject in scene first, then save as prefab";
        }

        #endregion
    }
}
