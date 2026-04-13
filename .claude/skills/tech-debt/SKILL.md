---
name: tech-debt
description: "Scans codebase for technical debt indicators (TODO, FIXME, HACK, complexity), tracks and prioritizes debt items."
argument-hint: "[scan|report]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

When this skill is invoked:

1. **Determine mode**:
   - `scan`: Search codebase for debt indicators
   - `report`: Generate summary from previous scans

2. **For `scan` mode**:
   - Search for TODO, FIXME, HACK, TEMP, WORKAROUND comments
   - Identify files > 300 lines (complexity risk)
   - Find methods > 40 lines
   - Detect code duplication patterns
   - Check for missing error handling in MCP tools
   - Flag deprecated Unity API usage

3. **Categorize debt**:
   - **Architecture**: Wrong patterns, tight coupling
   - **Code Quality**: Complex methods, duplication
   - **Documentation**: Missing descriptions on MCP tools
   - **Testing**: Untested systems
   - **Performance**: Known slow paths without optimization
   - **Dependency**: Outdated packages

4. **Output**:
```
## Tech Debt Report — [Date]

### Summary
- Total items: X
- Critical: X | Major: X | Minor: X

### By Category
| Category | Count | Top Item |
|----------|-------|----------|
| Architecture | X | ... |
| Code Quality | X | ... |
| ...

### Top 5 Priority Items
1. [Item] — Impact: HIGH, Effort: LOW (quick win)

### Aging Alert
[Items older than 30 days]
```
