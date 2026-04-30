---
name: estimate
description: "Estimates task effort by analyzing complexity, dependencies, and risk. Produces structured estimates with confidence levels."
argument-hint: "[task-description or T-XXX]"
user-invocable: true
allowed-tools: Read, Glob, Grep
---

When this skill is invoked:

1. **Identify the task**: Read task from docs/TASKS.md or parse description.

2. **Analyze complexity**:
   - Lines of code estimate
   - Number of files affected
   - Dependencies on other systems
   - Need for Unity API research
   - MCP tool integration complexity
   - Reference code available? (check reference repos)

3. **Assess risks**:
   - Unknown Unity APIs
   - Threading/MainThread complexity
   - Platform-specific concerns
   - Integration testing needs

4. **Output estimate**:
```
## Estimate: [Task]

### Effort Range
| Scenario | Hours | Confidence |
|----------|-------|------------|
| Optimistic | X | ... |
| Expected | X | ... |
| Pessimistic | X | ... |

### Complexity Factors
- [Factor]: [Impact]

### Dependencies
- [Dependency]: [Risk level]

### Recommendation
[Proceed / Break into subtasks / Spike first]
```
