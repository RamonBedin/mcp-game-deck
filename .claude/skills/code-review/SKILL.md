---
name: code-review
description: "Performs architectural and quality code review on Unity C# files. Checks coding standards, SOLID, Unity best practices, and performance."
argument-hint: "[path-to-file-or-directory]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---

When this skill is invoked:

1. **Read the target file(s)** in full.

2. **Read CLAUDE.md** for project coding standards.

3. **Identify the system category** (Tools, Resources, Prompts, ChatUI, Runtime, Agent SDK Server) and apply category-specific standards.

4. **Evaluate against coding standards**:
   - [ ] Namespace follows `GameDeck.Editor.*` pattern
   - [ ] Naming: PascalCase classes/methods, _camelCase private fields, camelCase locals
   - [ ] MCP tools use `[McpToolType]` and `[McpTool]` attributes
   - [ ] Tool descriptions in English via `[Description]`
   - [ ] Heavy work via `MainThreadDispatcher.Execute()`
   - [ ] No `System.Linq` — manual `for` loops always
   - [ ] No lambda delegates — use named methods or manual loops
   - [ ] No silent catch blocks — every `catch` must log or return `ToolResponse.Error()`
   - [ ] All asset paths validated to start with `"Assets/"`
   - [ ] `TryParse` instead of `Parse` for user input — never let `FormatException` propagate
   - [ ] `float.TryParse` uses `CultureInfo.InvariantCulture`
   - [ ] No method exceeds 40 lines
   - [ ] Dependencies injected, not static singletons

5. **Check Unity-specific issues**:
   - [ ] No `Find()`, `FindObjectOfType()`, `SendMessage()` in production code
   - [ ] No `GetComponent<>()` in `Update()`
   - [ ] `[SerializeField] private` instead of `public` fields
   - [ ] No allocations in hot paths
   - [ ] Frame-rate independence (delta time)
   - [ ] UI uses UI Toolkit only (no uGUI for new UI)

6. **Check architecture**:
   - [ ] Correct dependency direction
   - [ ] No circular dependencies
   - [ ] Events for cross-system communication
   - [ ] Follows patterns from ARCHITECTURE.md

7. **Output the review**:

```
## Code Review: [File/System Name]

### Standards Compliance: [X/7 passing]
[List failures with line references]

### Unity Best Practices: [CLEAN / ISSUES FOUND]
[List specific concerns]

### Architecture: [CLEAN / ISSUES FOUND]
[List specific violations]

### Positive Observations
[What is done well]

### Required Changes
[Must-fix items]

### Suggestions
[Nice-to-have improvements]

### Verdict: [APPROVED / APPROVED WITH SUGGESTIONS / CHANGES REQUIRED]
```
