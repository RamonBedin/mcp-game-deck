---
name: balance-check
description: "Analyzes game balance data (ScriptableObjects, configs) for outliers, broken progressions, and degenerate strategies."
argument-hint: "[system-name or path-to-data]"
user-invocable: true
allowed-tools: Read, Glob, Grep
---

When this skill is invoked:

1. **Read balance data**: ScriptableObjects, config files, formula implementations.

2. **Analyze by domain**:
   - **Combat**: DPS ranges, time-to-kill, damage type effectiveness
   - **Economy**: Resource flow (sources vs sinks), inflation risk
   - **Progression**: XP curves, power scaling, dead zones
   - **Loot/Drops**: Drop rates, expected time-to-acquire

3. **Check for**:
   - Outliers (values 2+ standard deviations from mean)
   - Degenerate strategies (one dominant approach)
   - Dead zones (progression feels stalled)
   - Infinite loops (economy exploits)
   - Power curves that flatten or spike unexpectedly

4. **Output**:
```
## Balance Check: [System]

### Data Summary
[Key ranges and distributions]

### Outliers Found
| Value | Expected Range | Actual | Impact |
|-------|---------------|--------|--------|

### Balance Concerns
1. [Concern]: [Evidence and recommendation]

### Verdict: [BALANCED / MINOR TUNING / REBALANCE NEEDED]
```
