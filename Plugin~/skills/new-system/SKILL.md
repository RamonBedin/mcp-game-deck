---
name: new-system
description: "Generate a complete Unity system — MonoBehaviour + ScriptableObject config from structured input."
argument-hint: "[system-name]"
user-invocable: true
allowed-tools: Read, Write, Glob, Grep
---

When this skill is invoked:

1. **Parse the system name** from the user's message. If the user wrote
   `/new-system SpawnSystem`, the name is `SpawnSystem`. If no name was
   provided, ask for one. The name should be PascalCase and end with
   `System`, `Manager`, or `Controller` (e.g., `SpawnSystem`,
   `InventoryManager`, `InputController`).

2. **Ask for the following inputs** before generating anything:

   - **Responsibilities**: "What does this system do? Describe in 1-2 sentences."
   - **Dependencies**: "Does it interact with other systems? If so, which ones?"
   - **Data-driven?**: "Should it use ScriptableObjects for configuration data?
     (e.g., spawn rates, inventory slot counts, damage curves)"

   Wait for the user to answer all three before proceeding.

3. **Read knowledge base docs** for architecture guidance:
   - `${CLAUDE_PLUGIN_ROOT}/knowledge/02-scriptableobjects-data-driven.md` — for ScriptableObject patterns
   - `${CLAUDE_PLUGIN_ROOT}/knowledge/03-unity-design-patterns.md` — for system design patterns

4. **Generate the system class** following these conventions:
   - Namespace: `[ProjectName].Systems` (or appropriate sub-namespace)
   - MonoBehaviour if it needs lifecycle (Update, OnEnable) or scene presence
   - Static class if it's a pure utility with no state
   - `[SerializeField] private` for inspector fields, never public
   - Events (System.Action or ScriptableObject event channels) for cross-system communication
   - No `Find()`, `FindObjectOfType()`, or `SendMessage()` — use dependency injection or serialized references
   - No method exceeds 40 lines
   - Manual `for` loops — no System.Linq
   - Include XML doc comments on the class explaining its purpose

5. **Generate the ScriptableObject config** (if data-driven):
   - Separate `[CreateAssetMenu]` class for configuration data
   - Name it `<SystemName>Config` (e.g., `SpawnSystemConfig`)
   - Include sensible default values
   - Add `[Tooltip]` attributes on fields

6. **Reference assembly definitions**: if the system belongs to a specific
   layer (Runtime, Infrastructure, etc.), mention which `.asmdef` it should
   live under, per the 4-layer architecture in KB doc 01.

7. **Write the files** using the Write tool to the appropriate location
   in the user's project. Ask the user for the target folder if unclear.

8. **Summary**: list the generated files, their purpose, and any manual
   steps the user needs to take (e.g., creating the ScriptableObject asset
   in the Editor, wiring references).