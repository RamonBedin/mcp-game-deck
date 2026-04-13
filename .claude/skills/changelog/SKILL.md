---
name: changelog
description: "Auto-generates changelog from git commits and task completion status."
argument-hint: "[version]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---

When this skill is invoked:

1. **Read git log** since last tag/version.

2. **Read `docs/TASKS.md`** for completed tasks context.

3. **Categorize changes**:
   - **New Tools**: New MCP tools added
   - **New Features**: Agents, skills, UI features
   - **Improvements**: Enhancements to existing features
   - **Bug Fixes**: Fixes
   - **Internal**: Refactoring, docs, CI

4. **Generate changelog**:
```
## [Version] — [Date]

### New MCP Tools
- `tool-name` — description (T-XXX)

### New Features
- description (T-XXX)

### Improvements
- description

### Bug Fixes
- description

### Internal
- description
```
