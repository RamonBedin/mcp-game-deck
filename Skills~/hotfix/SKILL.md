---
name: hotfix
description: "Emergency fix workflow with audit trail. For S1/S2 bugs that need immediate resolution."
argument-hint: "[bug-description]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write, Edit, Bash
---

When this skill is invoked:

1. **Verify severity**: This workflow is for S1 (Critical) and S2 (Major) bugs only.

2. **Diagnose**:
   - Read relevant code and error logs
   - Use MCP tools if needed (profiler-status/profiler-memory for crashes, reflect-get-type/reflect-search for type issues)
   - Identify root cause and affected systems

3. **Implement minimal fix**:
   - Change ONLY what's necessary to fix the bug
   - No refactoring, no improvements, no cleanup
   - Add a comment: `// HOTFIX: [description] — [date]`

4. **Verify fix**:
   - Ensure the fix resolves the issue
   - Check for regressions in adjacent systems
   - Use MCP tools to verify (build, profiler, screenshots)

5. **Create hotfix record**:
```
## Hotfix: [Title]
### Date: [YYYY-MM-DD]
### Severity: [S1/S2]

### Problem
[What was broken]

### Root Cause
[Why it was broken]

### Fix
[What was changed — files and lines]

### Verification
[How the fix was verified]

### Follow-Up
[Tech debt items created, tests to add, cleanup needed]
```

6. **Commit** with format: `fix(HOTFIX): description`
