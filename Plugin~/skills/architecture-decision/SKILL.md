---
name: architecture-decision
description: "Creates an Architecture Decision Record (ADR) documenting a technical decision, alternatives, and consequences."
argument-hint: "[title]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

When this skill is invoked:

1. **Gather context**: Read ARCHITECTURE.md and relevant code/docs.

2. **Create ADR** with this structure:
```
## ADR-[NNN]: [Title]

### Status: [Proposed | Accepted | Superseded]
### Date: [YYYY-MM-DD]

### Context
[What is the issue? Why does this decision need to be made?]

### Decision
[What was decided and why]

### Alternatives Considered
| Option | Pros | Cons |
|--------|------|------|
| [A] | ... | ... |
| [B] | ... | ... |

### Consequences
**Positive:**
- ...

**Negative:**
- ...

**Risks:**
- ...

### Unity-Specific Considerations
[How this interacts with Unity's architecture, MCP tools, etc.]

### Validation
[How to verify this decision was correct]
```

3. **Save** to `docs/adr/ADR-NNN-title.md`
