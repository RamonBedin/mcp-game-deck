---
name: design-review
description: "Reviews a game design document for completeness, consistency, implementability, and balance. Run before implementation."
argument-hint: "[path-to-design-doc]"
user-invocable: true
allowed-tools: Read, Glob, Grep
---

When this skill is invoked:

1. **Read the design document** in full.

2. **Check completeness** (all sections present):
   - [ ] Overview / summary
   - [ ] Player experience goal
   - [ ] Detailed mechanics/rules
   - [ ] Formulas (if numeric system)
   - [ ] Edge cases handled
   - [ ] Dependencies on other systems
   - [ ] Tuning knobs identified
   - [ ] Acceptance criteria

3. **Check consistency**:
   - No contradictions within the document
   - Numbers add up (economy flows, damage ranges)
   - References to other systems are accurate

4. **Check implementability** with our stack:
   - Can this be built with Unity 6 + URP?
   - Which MCP tools would be needed?
   - ScriptableObject data model feasible?
   - Performance implications clear?

5. **Output**:
```
## Design Review: [Document Name]

### Completeness: [X/8 sections present]
[Missing sections listed]

### Consistency: [CONSISTENT / ISSUES FOUND]
[Contradictions or gaps]

### Implementability: [CLEAR / CONCERNS]
[Technical feasibility with our Unity stack]

### Balance Risks
[Obvious balance concerns]

### Verdict: [APPROVED / NEEDS REVISION / MAJOR REVISION]

### Action Items
1. ...
```
