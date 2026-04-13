#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;

namespace GameDeck.Editor.Prompts
{
    /// <summary>
    /// MCP Prompt that provides a structured workflow for creating UI with UI Toolkit —
    /// UXML layout, USS styles, and C# backing code following MVP/MVVM patterns.
    /// </summary>
    public class Prompt_UIToolkitWorkflow
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Returns the UI Toolkit workflow prompt for the given screen or panel name.
        /// </summary>
        /// <param name="screenName">Name or description of the UI screen to create (e.g. 'MainMenu', 'InventoryPanel', 'HUD').</param>
        /// <returns>A formatted prompt string instructing the AI on the correct UI Toolkit workflow.</returns>
        [McpPrompt(Name = "ui-toolkit-workflow")]
        [Description("Workflow for creating UI with UI Toolkit — UXML layout, USS styles, and C# backing code.")]
        public string UIToolkitWorkflow([Description("Name or description of the UI screen to create (e.g. 'MainMenu', 'InventoryPanel', 'HUD').")] string screenName)
        {
            return $@"You are an expert MCP Game Deck specializing in UI Toolkit. Create ""{screenName}"" UI.

WORKFLOW:
1. Plan the UI layout structure
2. Create UXML file with uitoolkit-create-uxml
3. Create USS stylesheet with uitoolkit-create-uss
4. Write C# MonoBehaviour or EditorWindow that loads and binds the UI
5. Wire up event handlers and data binding
6. Test the UI

UXML BEST PRACTICES:
- Use semantic element names (PascalCase): <ui:VisualElement name=""HeaderPanel"">
- Use USS classes (kebab-case): class=""header-panel main-content""
- Prefer VisualElement containers over nested Labels
- Use ListView with makeItem/bindItem for lists (not manual ScrollView)
- Set picking-mode=""Ignore"" on decorative elements

USS BEST PRACTICES:
- Use USS variables for theming: var(--unity-colors-default-text)
- Support both light/dark mode — never hardcode colors
- Use flex-grow, flex-shrink for responsive layouts
- Keep USS modular — one file per screen/component

C# BEST PRACTICES:
- Query elements with Q<T>(""name"") or Q(className: ""class"")
- Use RegisterCallback<ClickEvent> not RegisterCallback<MouseDownEvent>
- For data binding, implement INotifyBindablePropertyChanged
- Follow MVP/MVVM — ViewModel never modifies game state directly
- Use Screen Stack pattern: Push/Pop/Replace for navigation

ARCHITECTURE (from knowledge base):
- Screen Stack for menu navigation
- MVP/MVVM separation
- Virtualized ListView for long lists (inventory, leaderboard)
- Safe area handling for mobile (notch, rounded corners)

TOOLS TO USE:
- uitoolkit-create-uxml — create UXML files
- uitoolkit-create-uss — create USS stylesheets
- uitoolkit-list — list existing UI assets
- uitoolkit-inspect-uxml — read existing UXML content
- script-update-or-create — write C# backing code
- recompile-scripts — compile after writing scripts

IMPORTANT: NEVER use uGUI (Canvas/Image/Button). Always use UI Toolkit.";
        }

        #endregion
    }
}
