---
name: sprint-plan
description: "Generates or updates a sprint plan based on docs/TASKS.md backlog, completed work, and priorities."
argument-hint: "[new|update|status]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit
---

When this skill is invoked:

1. **Read `docs/TASKS.md`** to get the full backlog with status and priorities.

2. **Determine mode**:
   - `new`: Create a new sprint plan
   - `update`: Update existing sprint plan with progress
   - `status`: Report current sprint status

3. **For `new` mode**:
   - Identify all ⬜ tasks ordered by priority (🔴 > 🟡 > 🟢)
   - Follow the critical path from TASKS.md
   - Group into a sprint with Must/Should/Nice categories
   - Output sprint plan with task list and dependencies

4. **For `status` mode**:
   - Count ✅ vs ⬜ vs 🟡 tasks
   - Calculate completion percentage per phase
   - Identify blockers and risks
   - Report critical path progress

5. **Output format**:
```
## Sprint Plan — [Date]

### Must Have (🔴)
- [ ] T-XXX: Description

### Should Have (🟡)
- [ ] T-XXX: Description

### Nice to Have (🟢)
- [ ] T-XXX: Description

### Dependencies
[Task dependency chain]

### Risks
[Identified risks and mitigations]
```
