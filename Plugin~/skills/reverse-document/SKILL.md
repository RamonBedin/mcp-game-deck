---
name: reverse-document
description: "Generates design or architecture documentation from existing code. Works backwards from implementation to create missing docs."
argument-hint: "[design|architecture] [path]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash
---

When this skill is invoked:

1. **Determine type**: `design` (gameplay doc) or `architecture` (technical doc).

2. **Analyze code**:
   - Read all files in the target path
   - Map class relationships and dependencies
   - Identify patterns used (state machines, events, pooling)
   - Extract config values and their ranges
   - Note MCP tool integrations

3. **For design docs**: Extract gameplay rules, formulas, configs, and edge cases from code.

4. **For architecture docs**: Extract component diagram, data flow, dependencies, and patterns.

5. **Generate document** with metadata indicating it was reverse-engineered:
```
## [Title] (Reverse-Documented)

> Generated from code analysis on [date].
> Review for accuracy — code intent may differ from design intent.

[Full document following standard structure]

### Open Questions
[Ambiguities found in the code that need clarification]

### Suggested Improvements
[Issues found during analysis]
```
