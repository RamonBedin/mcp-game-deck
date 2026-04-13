#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;

namespace GameDeck.Editor.Prompts
{
    /// <summary>
    /// MCP Prompt that provides a structured workflow for setting up a new Unity scene
    /// with proper hierarchy organization, lighting, camera, and environment configuration.
    /// </summary>
    public class Prompt_SceneSetup
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Returns the scene setup prompt tailored to the requested scene type.
        /// </summary>
        /// <param name="sceneType">The type of scene to set up: gameplay, menu, loading, test, or empty.</param>
        /// <returns>A formatted prompt string instructing the AI on the correct setup workflow.</returns>
        [McpPrompt(Name = "scene-setup")]
        [Description("Workflow for setting up a new Unity scene with proper structure — lighting, camera, environment, and organization.")]
        public string SceneSetup([Description("Type of scene to set up: gameplay, menu, loading, test, empty")] string sceneType)
        {
            return $@"You are an expert MCP Game Deck. Set up a new scene of type ""{sceneType}"".

WORKFLOW:
1. Check current scene state via mcp-game-deck://scenes-hierarchy
2. Create scene organization structure with empty parent GameObjects:
   - ""--- Environment ---"" (lighting, skybox, terrain)
   - ""--- Gameplay ---"" (player, enemies, interactables)
   - ""--- UI ---"" (canvases, HUD)
   - ""--- Managers ---"" (GameManager, AudioManager, etc.)

3. Based on scene type:

   GAMEPLAY scene:
   - Set up Main Camera with proper position and clear flags
   - Add Directional Light with shadows
   - Create ground plane or terrain
   - Add player spawn point (empty GameObject)
   - Configure physics and lighting settings

   MENU scene:
   - Set up UI Camera (orthographic or perspective for 3D menus)
   - Create UI Toolkit document or Canvas
   - Add EventSystem
   - Minimal lighting

   LOADING scene:
   - Minimal setup — camera + loading UI
   - No gameplay objects

   TEST scene:
   - Simple lighting
   - Test-friendly camera
   - Clean ground plane

4. Verify setup via scenes-hierarchy resource
5. Save the scene

TOOLS TO USE:
- camera-create, camera-configure — set up cameras
- gameobject-create — create organization objects
- graphics-get-settings — verify render pipeline
- build-manage-scenes — add scene to build settings

GUIDELINES:
- Follow the 4-layer architecture: Core → Gameplay → UI → Infrastructure
- Use descriptive names with separator GameObjects (--- Category ---)
- Configure quality settings appropriate to the scene type";
        }

        #endregion
    }
}
