# Reports

Output from the refactor pipeline. Four subfolders, one per stage that produces a file:

```
.claude/reports/
├── audits/         ← tool-auditor output       (diagnostic — what's wrong?)
├── reviews/        ← Ramon's decisions         (which findings to act on?)
├── plans/          ← consolidation-planner     (how do we fix the accepted findings?)
└── validations/    ← build-validator           (did the consolidator's changes hold up?)
```

## Naming

All files use `[stage]-[domain]-[YYYYMMDD].md`. Examples:

- `audits/audit-Animation-20260417.md`
- `reviews/review-Animation-20260417.md`
- `plans/plan-Animation-20260417.md`
- `validations/validation-Animation-20260417.md`

The matching dates make it easy to trace one cycle through the pipeline.

## When to start a new cycle

A "cycle" is one pass through audit → review → plan → consolidate → validate for one domain. Start a fresh cycle (with a new date) when:

- Significant time has passed since the last audit (e.g. months, code has drifted)
- You've completed consolidation and want to re-audit to confirm the fixes landed cleanly
- New findings emerge that weren't in the prior audit

Within a single cycle, never overwrite earlier-stage files. If the audit needs corrections, prefer to either:
- Add a new audit (today's date) and supersede the old one
- Add a `*-amendment.md` file alongside

This keeps the historical record honest.

## What's NOT in this folder

The `tool-consolidator` doesn't write a file here — its deliverable is the actual edits to `.cs` files in `Editor/Tools/`, reviewed via VS Code Source Control.

Git history is also not stored here. Ramon's commits via VS Code are the canonical record of what landed.

## Versioning

These files are versioned in git intentionally. The progression of audits, plans, and validations over time is useful signal — they show how the codebase evolved and which problems were caught/fixed/missed. If the directory gets unwieldy in the future, archive old cycles to a `.claude/reports/archive/` subfolder rather than deleting.
