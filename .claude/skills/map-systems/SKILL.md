---
name: map-systems
description: "Decomposes a game concept into individual systems, maps dependencies, and prioritizes design/implementation order."
argument-hint: "[concept-doc-path or description]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit
---

When this skill is invoked:

1. **Read the concept** (design doc or description).

2. **Extract explicit systems**: Systems directly mentioned.

3. **Identify implicit systems**: Systems implied but not stated (e.g., inventory implies item database, save system implies serialization).

4. **Map dependencies**: Which systems depend on which.

5. **Prioritize**:
   - MVP (must have for core loop)
   - Vertical Slice (needed for demo)
   - Alpha (needed for playable game)
   - Full Vision (nice to have)

6. **Output**:
```
## Systems Map: [Concept]

### Systems Identified
| System | Type | Priority | Dependencies |
|--------|------|----------|--------------|
| ... | Explicit/Implicit | MVP/VS/Alpha/Full | ... |

### Dependency Graph
[Text representation of dependencies]

### Recommended Implementation Order
1. [System] — no dependencies, MVP
2. [System] — depends on #1, MVP
...

### Design Order (design these first)
1. [System] — most depended upon
```
