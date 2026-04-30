---
name: perf-profile
description: "Structured performance profiling workflow for Unity. Identifies bottlenecks, measures against budgets, and recommends optimizations."
argument-hint: "[system-name or 'full']"
user-invocable: true
allowed-tools: Read, Glob, Grep, Bash
---

When this skill is invoked:

1. **Identify target**: Specific system or full project scan.

2. **Use MCP profiler tools** (profiler-status, profiler-memory, profiler-extended):
   - Start profiler, capture frame timing data
   - Take memory snapshots
   - Enable frame debugger for draw call analysis

3. **Analyze code** for performance anti-patterns:
   - Allocations in Update/FixedUpdate/hot paths
   - Missing object pooling
   - Uncached GetComponent calls
   - LINQ in hot paths
   - String concatenation in loops
   - Unnecessary Update() methods

4. **Check against budgets**:
   - Frame time: 16.6ms (60fps) or 33.3ms (30fps)
   - Draw calls: < 2000 PC, < 500 mobile
   - Memory: varies by platform
   - GC: < 1KB/frame in gameplay

5. **Output report**:
```
## Performance Profile: [System/Full]

### Hotspots Found
| Location | Issue | Impact | Fix Effort |
|----------|-------|--------|------------|
| file:line | description | HIGH/MED/LOW | hours |

### Quick Wins (< 1 hour each)
1. ...

### Major Optimizations Needed
1. ...

### Memory Concerns
[Snapshot analysis, leak suspects]

### Recommendations (Priority Order)
1. ...
```
