---
name: asset-audit
description: "Audits Unity assets for naming conventions, file size budgets, format standards, and orphaned/missing references."
argument-hint: "[category|all]"
user-invocable: true
allowed-tools: Read, Glob, Grep
---

When this skill is invoked:

1. **Identify scope**: Specific category or full audit.

2. **Check naming conventions**:
   - Scripts: PascalCase.cs
   - UXML: PascalCase.uxml
   - USS: PascalCase.uss or kebab-case.uss
   - Prefabs: PascalCase.prefab
   - ScriptableObjects: PascalCase.asset
   - Textures: T_[Name]_[Type] (e.g., T_Character_Diffuse)
   - Shaders: SG_[Category]_[Name]
   - VFX: VFX_[Category]_[Name]

3. **Check standards**:
   - Textures: power-of-2 dimensions, correct compression format
   - Scripts: assembly definition coverage
   - MCP tools: [Description] attributes present
   - Package.json: valid metadata

4. **Find orphans**: Assets with no code references.

5. **Find missing**: Code references to non-existent assets.

6. **Output**:
```
## Asset Audit: [Category/All]

### Naming Violations
| Asset | Issue | Suggested Fix |
|-------|-------|---------------|

### Standard Violations
| Asset | Issue | Impact |
|-------|-------|--------|

### Orphaned Assets
[Assets with no references]

### Missing References
[Code references to non-existent assets]

### Summary
- Scanned: X assets
- Violations: X
- Verdict: [CLEAN / NEEDS CLEANUP / CRITICAL ISSUES]
```
