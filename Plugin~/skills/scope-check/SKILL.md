---
name: scope-check
description: "Analyzes a feature or phase for scope creep. Compares current scope against the original plan in TASKS.md."
argument-hint: "[feature-name or phase-N]"
user-invocable: true
allowed-tools: Read, Glob, Grep
---

When this skill is invoked:

1. **Read `docs/TASKS.md`** for the original plan.
2. **Read `docs/ARCHITECTURE.md`** for the planned scope.
3. **Scan current implementation** to identify any extras.

4. **Compare**:
   - What was planned vs what exists
   - Any unplanned additions
   - Any missing planned features
   - Scope bloat percentage

5. **Output**:
```
## Scope Check: [Feature/Phase]

### Original Plan
[Tasks/features from TASKS.md]

### Current State
[What's actually been built]

### Additions (Not in Original Plan)
- [Addition]: [Justification?]

### Missing (In Plan but Not Built)
- [Missing item]: [Reason/Blocker]

### Scope Assessment
- Bloat: [X%]
- Verdict: [On Track / Minor Creep / Significant Creep]

### Recommendations
- Cut: [items to remove]
- Defer: [items for later]
- Keep: [justified additions]
```
