---
name: new-feature
description: "Plan a feature implementation with tasks, architecture, and file list."
argument-hint: "[feature-name]"
user-invocable: true
allowed-tools: Read, Glob, Grep
---

When this skill is invoked:

1. **Parse the feature name** from the user's message. If the user wrote
   `/new-feature InventorySystem`, the name is `InventorySystem`. If no
   name was provided, ask for one.

2. **Ask for the following inputs** before generating anything:

   - **User story**: "Describe the feature as a user story: 'As a player,
     I want to ... so that ...'"
   - **Acceptance criteria**: "What defines 'done'? List the conditions
     that must be true for this feature to be complete."
   - **Scope**: "What's the scope? (small = 1-2 files, medium = 3-6 files,
     large = 7+ files or cross-cutting)"

   Wait for the user to answer all three before proceeding.

3. **Scan the existing codebase** to understand the current architecture:
   - Use Glob to find related scripts, prefabs, and ScriptableObjects
   - Use Grep to find references to related systems
   - Read relevant existing files to understand current patterns

4. **Read knowledge base docs** relevant to the feature domain:
   - Always read `${CLAUDE_PLUGIN_ROOT}/knowledge/01-unity-project-architecture.md` for project structure
   - Read additional KB docs based on feature type (e.g., doc 07 for gameplay,
     doc 08 for UI, doc 02 for data-driven systems)

5. **Generate the implementation plan** with this structure:

   ```
   ## Feature Plan: [Feature Name]

   ### User Story
   [The user story from step 2]

   ### Acceptance Criteria
   [Numbered list from step 2]

   ### Architecture
   - **Pattern(s)**: [Which design patterns to use, from KB doc 03]
   - **Systems involved**: [New and existing systems]
   - **Data model**: [ScriptableObjects, serialized classes, or runtime data]
   - **Communication**: [Events, direct references, or message bus]

   ### Tasks
   [Ordered list of implementation tasks, each with]:
   - [ ] Task description
     - Files: `path/to/file.cs` (create / modify)
     - Depends on: [previous task number, if any]

   ### Files to Create
   | File | Purpose |
   |------|---------|
   | `path/to/NewFile.cs` | Description |

   ### Files to Modify
   | File | Change |
   |------|--------|
   | `path/to/ExistingFile.cs` | What to change |

   ### Complexity Estimate
   - **Scope**: [small / medium / large]
   - **Risk areas**: [What could go wrong]
   - **Suggested approach**: [Bottom-up, top-down, or spike-first]

   ### KB References
   - [List relevant KB docs with brief reason for each]
   ```

6. **Present the plan** and ask if the user wants to proceed with
   implementation, adjust the plan, or save it as a design document.