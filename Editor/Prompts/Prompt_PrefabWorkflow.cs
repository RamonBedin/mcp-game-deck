#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;

namespace GameDeck.Editor.Prompts
{
    /// <summary>
    /// MCP Prompt that provides a structured workflow for creating, editing,
    /// and managing Unity prefabs — from scene object to reusable asset.
    /// </summary>
    public class Prompt_PrefabWorkflow
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Returns the prefab workflow prompt for the given prefab name or asset path.
        /// </summary>
        /// <param name="prefabName">The prefab name or asset path to work with.</param>
        /// <returns>A formatted prompt string instructing the AI on the correct prefab workflow.</returns>
        [McpPrompt(Name = "prefab-workflow")]
        [Description("Workflow for creating, editing, and managing prefabs — from scene object to reusable asset.")]
        public string PrefabWorkflow([Description("The prefab name or asset path to work with.")] string prefabName)
        {
            return $@"You are an expert MCP Game Deck. Handle prefab workflow for ""{prefabName}"".

WORKFLOW:

CREATING A NEW PREFAB:
1. Create the GameObject in scene with all components configured
2. Use script-update-or-create to write any needed MonoBehaviour scripts
3. Wait for compilation (recompile-status to verify)
4. Add custom components to the GameObject
5. Configure all serialized fields via update_component
6. Save as prefab using create_prefab tool
7. Verify the prefab asset exists via mcp-game-deck://assets/t:Prefab

EDITING AN EXISTING PREFAB:
1. Check if prefab exists: mcp-game-deck://assets/t:Prefab filter by name
2. Instantiate into scene with add-asset-to-scene
3. Make modifications (add/remove components, change values)
4. Apply changes back to prefab (apply_prefab_overrides)
5. Remove the scene instance if no longer needed

PREFAB VARIANTS:
1. Load base prefab with add-asset-to-scene
2. Modify only the variant-specific properties
3. Save as new prefab variant

GUIDELINES:
- Keep prefabs self-contained — all references should be serialized
- Use ScriptableObjects for shared configuration (scriptableobject-create)
- Nested prefabs are preferred over monolithic prefabs
- Always verify scripts compile before adding them as components
- Test the prefab by instantiating and checking in Play mode

TOOLS TO USE:
- add-asset-to-scene — instantiate prefabs
- scriptableobject-create/modify — create config assets
- recompile-scripts, recompile-status — ensure scripts compile
- reflect-get-type — verify component types exist";
        }

        #endregion
    }
}
