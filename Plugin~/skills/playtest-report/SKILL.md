---
name: playtest-report
description: "Generates structured playtest report template or analyzes playtest notes into actionable format."
argument-hint: "[new|analyze path-to-notes]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

When this skill is invoked:

1. **Determine mode**:
   - `new`: Generate blank template
   - `analyze [path]`: Parse raw notes into structured format

2. **Template structure**:
```
## Playtest Report — [Date]

### Session Info
- Build: [version]
- Duration: [minutes]
- Platform: [target]
- Tester: [name/anonymous]

### First Impressions (0-5 min)
[Initial reactions, onboarding clarity, first confusion points]

### Gameplay Flow
**What Worked:**
- ...

**Pain Points:**
- ...

**Confusion Points:**
- ...

**Delight Moments:**
- ...

### Bugs Encountered
| Bug | Severity | Reproducible? |
|-----|----------|---------------|

### Feature-Specific Feedback
[Per-feature observations]

### Performance Notes
[Frame drops, loading times, visual glitches]

### Overall Assessment
- Fun Factor: [1-5]
- Polish Level: [1-5]
- Would Play Again: [Yes/No/Maybe]

### Priority Actions
1. [Most impactful change to make]
```

3. **For `analyze` mode**: Parse raw notes and categorize into the structured format above, extracting bugs, pain points, and priority actions.
