---
name: bug-report
description: "Creates structured bug reports or analyzes code to identify potential bugs. Ensures full reproduction steps and severity assessment."
argument-hint: "[description] or analyze [path]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

When this skill is invoked:

1. **Determine mode**:
   - Description provided → create bug report
   - `analyze [path]` → scan code for potential bugs

2. **For bug report creation**:
```
## Bug Report: [Title]

### Severity: [S1-Critical | S2-Major | S3-Minor | S4-Trivial]
### Priority: [P1-Immediate | P2-Next Sprint | P3-Backlog]

### Description
[Clear description of the bug]

### Reproduction Steps
1. ...

### Expected Behavior
[What should happen]

### Actual Behavior
[What actually happens]

### Technical Context
[Relevant code paths, systems involved, MCP tools that can verify]

### Suggested Fix
[If apparent from code analysis]
```

3. **For code analysis** (`analyze` mode):
   - Scan for null reference risks
   - Check for race conditions (threading + MainThread)
   - Look for resource leaks (missing Dispose, Release)
   - Check Unity-specific pitfalls (destroyed object access, coroutine leaks)
   - Verify MCP tool error handling (ToolResponse.Error paths)
