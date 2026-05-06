---
name: prototype
description: "Rapid prototyping workflow for Unity. Quickly validates a mechanic or concept with throwaway code and a structured report."
argument-hint: "[concept-description]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash
---

When this skill is invoked:

1. **Define hypothesis**: What are we testing? What does success look like?

2. **Rapid implementation** (throwaway code, skip normal standards):
   - Create scripts in a `Prototypes/` folder
   - Use MCP tools to set up scene quickly (batch-execute, add-asset-to-scene)
   - Use ScriptableObjects for quick config via scriptableobject-create/scriptableobject-modify
   - Skip documentation, skip tests, skip polish

3. **Validate**:
   - Use screenshot-camera to capture results
   - Use profiler-status/profiler-extended if performance is part of the hypothesis
   - Compare expected vs actual behavior

4. **Generate prototype report**:
```
## Prototype Report: [Name]

### Hypothesis
[What we were testing]

### Approach
[How we tested it — scripts created, MCP tools used]

### Results
[What happened — screenshots, profiler data, observations]

### Verdict: [PROCEED / PIVOT / KILL]

### Next Steps
[If PROCEED: what needs production-quality implementation]
[If PIVOT: what alternative to try]
[If KILL: why and what we learned]
```

5. **Cleanup note**: Prototype code is disposable. Do NOT merge into production.
