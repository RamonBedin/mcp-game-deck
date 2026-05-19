# Review Template

> **How to use this template:**
> 1. Copy this file to `.claude/reports/reviews/review-[domain]-[YYYYMMDD].md`
> 2. Open the matching audit at `.claude/reports/audits/audit-[domain]-[YYYYMMDD].md` side-by-side
> 3. Fill out every section. Empty sections cause the planner to make assumptions.
> 4. Only after this file is complete should you invoke `consolidation-planner`.

---

# Review — [Domain]

**Audit reviewed:** `.claude/reports/audits/audit-[domain]-[YYYYMMDD].md`
**Reviewer:** Ramon
**Date:** YYYY-MM-DD

---

## 1. Decisions Per Finding

Mark each finding from the audit with a status. Use the same finding IDs (G1, R1, A2, D3, etc) from the audit.

| Finding ID | Decision | Notes |
|-----------|----------|-------|
| G1 | accept | — |
| G2 | accept-with-modification | scope down to parameters only, defer transition conditions |
| G3 | reject | not in scope for v1.2 |
| ... | ... | ... |

**Decision values:**
- `accept` — finding is correct, fix it as described
- `accept-with-modification` — finding is correct but scope/approach should change. Use Notes to explain.
- `reject` — disagree with the finding, do not act on it. Use Notes to explain why.
- `defer` — valid finding but not for this consolidation cycle. Will be revisited later.

---

## 2. Open Questions Answered

The audit's Section 7 lists open questions for the reviewer. Answer each here.

| Question | Answer |
|----------|--------|
| [paste question from audit Section 7] | [your answer] |
| ... | ... |

---

## 3. Constraints For The Plan

Tell the planner what's off-limits, what's preferred, and what's required. Be concrete.

### Backward Compatibility
- [ ] Must preserve all existing tool names (no breaking renames)
- [ ] May rename tools but must add deprecation aliases
- [x] May break tool names freely (this is internal refactor)

### Code Style
- Follow existing CLAUDE.md C# standards
- Prefer action-dispatched consolidation (see `Tool_Animation.ConfigureController.cs` pattern)
- [Add any project-specific guidance here]

### Scope Limits
- Do NOT touch [domain X] in this cycle
- Do NOT add new Unity API dependencies beyond [list]
- [Other no-go zones]

### Preferences
- [E.g. "Prefer macro tools that wrap full workflows over many small additions"]
- [E.g. "Description-only fixes are welcome — those are quick wins"]

---

## 4. Priority Override

The audit ranked findings by `Impact × (6 - Effort)`. If you want a different order for the consolidation cycle, list it here. Otherwise the planner uses the audit's ranking.

**Override order (top to bottom = first to last):**
1. [Finding ID] — [why first]
2. [Finding ID] — [why next]
3. ...

Or: `Use audit ranking as-is.`

---

## 5. Change Group Hints (Optional)

If you already see how findings should be grouped into PRs, sketch it here. The planner will respect your grouping. If left blank, the planner groups them itself.

### Group A — [Name]
- Findings: G1, G8
- Rationale: [why these belong together]

### Group B — [Name]
- Findings: D1, A1
- Rationale: ...

---

## 6. Notes For The Planner

Anything else the planner should know — context, prior decisions, related work in flight, things to avoid.

[Free-form text.]

---

## 7. Approval

Once you've filled out sections 1-6, mark this review as ready:

**Status:** ⏳ DRAFT  *(or)*  ✅ READY FOR PLANNING

When status is READY, invoke the `consolidation-planner` agent with this review file as input.
