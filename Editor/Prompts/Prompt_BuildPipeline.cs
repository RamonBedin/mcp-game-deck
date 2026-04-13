#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;

namespace GameDeck.Editor.Prompts
{
    /// <summary>
    /// MCP Prompt that provides a structured workflow for configuring and executing Unity builds,
    /// covering single-platform and multi-platform batch scenarios.
    /// </summary>
    public class Prompt_BuildPipeline
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Returns the build pipeline workflow prompt for the given target platform.
        /// </summary>
        /// <param name="targetPlatform">Target platform(s) to build for: windows64, android, ios, webgl, or 'all' for batch builds.</param>
        /// <returns>A formatted prompt string instructing the AI on the correct build pipeline workflow.</returns>
        [McpPrompt(Name = "build-pipeline")]
        [Description("Workflow for configuring and executing Unity builds — single platform or multi-platform batch builds.")]
        public string BuildPipeline([Description("Target platform(s) to build for: windows64, android, ios, webgl, or 'all' for batch.")] string targetPlatform)
        {
            return $@"You are an expert MCP Game Deck. Configure and execute a build for ""{targetPlatform}"".

WORKFLOW:

PRE-BUILD CHECKS:
1. Get current build settings: build-get-settings
2. Check for compilation errors: recompile-status
3. Verify scenes in build: build-manage-scenes action:list
4. Review graphics settings: graphics-get-settings

CONFIGURE BUILD:
1. Set required build properties:
   - build-set-settings property:product_name value:""YourGame""
   - build-set-settings property:version value:""1.0.0""
   - build-set-settings property:bundle_id value:""com.company.game""
2. Configure scenes in correct order (main menu first):
   - build-manage-scenes action:add scenePath:""Assets/Scenes/MainMenu.unity""
   - build-manage-scenes action:reorder scenePath:""Assets/Scenes/MainMenu.unity"" index:0
3. Set platform-specific settings:
   - For mobile: build-set-settings property:scripting_backend value:il2cpp
   - For development: build-set-settings property:development value:true

SINGLE PLATFORM BUILD:
1. Switch platform if needed: build-switch-platform target:{targetPlatform}
2. Execute build: build-player target:{targetPlatform}
3. Review build report (size, errors, warnings)

MULTI-PLATFORM BATCH BUILD:
1. build-batch targets:""windows64,osx,linux64"" outputDir:""Builds""
2. Review all results

PLATFORM-SPECIFIC GUIDELINES:
- Windows: .exe output, IL2CPP or Mono, StandaloneWindows64
- Android: .apk/.aab, IL2CPP required for Play Store, set minSdkVersion
- iOS: Xcode project output, IL2CPP only, requires Mac for final build
- WebGL: Compression recommended (LZ4), no threading, memory limits
- Mobile: ASTC textures, Vorbis audio, memory < 512MB budget

POST-BUILD:
1. Verify output files exist
2. Check build size against budget
3. Test the build (auto_run option for quick test)

TOOLS TO USE:
- build-get-settings, build-set-settings — configure
- build-switch-platform — change target
- build-player — single build
- build-batch — multi-platform
- build-manage-scenes — manage scene list
- recompile-status — check compilation
- graphics-get-settings — verify quality settings";
        }

        #endregion
    }
}
