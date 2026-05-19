# `.claude/agents/`

Specialized subagents for focused, repeatable tasks on MCP Game Deck.

## Status

| Agent | Role | Status | Purpose |
|-------|------|--------|---------|
| `tool-auditor` | subagent | ✅ ready, validated on Animation | Analyze a tool domain, produce diagnostic report. **Does not modify code.** |
| `auto-reviewer` | subagent | ✅ ready, not yet tested | Draft review file: auto-decide mechanical findings, escalate strategic ones to Ramon. **Does not modify code.** |
| `consolidation-planner` | subagent | ✅ ready, not yet tested | Take audit + finalized review, produce concrete refactor plan with C# signatures. **Does not modify code.** |
| `tool-consolidator` | subagent | ✅ ready, not yet tested | Execute approved plan: edit `.cs` files in `Editor/Tools/`. **No Bash, no git.** |
| `build-validator` | subagent | ✅ ready, not yet tested | Static convention checks + `dotnet build` (when csproj exists) + `tsc --noEmit`. **No git.** |
| `audit-batch-runner` | **orchestrator** (main session, not subagent) | ✅ ready, not yet tested | Run `tool-auditor` across many domains in one go. Resumable, state-tracked. |

**Note on `audit-batch-runner`:** unlike the others, this one runs as the main Claude Code session because subagents cannot invoke other subagents — and the batch runner needs `Task` to spawn `tool-auditor` instances.

## Pipeline

The full per-domain pipeline:

```
1. tool-auditor          → .claude/reports/audits/audit-[domain]-[YYYYMMDD].md
       ↓ (Ramon skims audit)

2. auto-reviewer         → .claude/reports/reviews/review-[domain]-[YYYYMMDD].md
       ↓ (Ramon answers 3-7 escalations directly in the file)
       ↓ (re-invoke auto-reviewer to finalize → Status: READY FOR PLANNING)

3. consolidation-planner → .claude/reports/plans/plan-[domain]-[YYYYMMDD].md
       ↓ (Ramon reviews plan, marks READY FOR EXECUTION)

4. tool-consolidator     → edits .cs files in Editor/Tools/
       ↓ (file edits land on disk)

5. build-validator       → .claude/reports/validations/validation-[domain]-[YYYYMMDD].md
       ↓ (Ramon reviews validation; if FAILED, decide: re-run consolidator or fix manually)

6. Ramon reviews diff in VS Code Source Control → commits via VS Code
```

For batch operations (e.g. auditing all 38 domains in one go), use `audit-batch-runner` instead of invoking `tool-auditor` 38 times manually.

**Never skip steps within a per-domain cycle.** The auto-reviewer keeps step 2 cheap (5-10 min of Ramon's time) without removing his strategic input.

## Invocation

From Claude Code:

```
/agents
```

Or natural language:

> "Use the tool-auditor on the Animation domain"
> "Use the audit-batch-runner to audit all domains except Helpers"
> "Use the auto-reviewer on the Animation audit"
> [Ramon answers escalations in the file]
> "Use the auto-reviewer to finalize the Animation review"
> "Use consolidation-planner with the Animation audit and review from 2026-04-17"
> "Use tool-consolidator with the Animation plan from 2026-04-17"
> "Use build-validator on the Animation changes"

## Design Principles

1. **Narrow mission.** Each agent does ONE thing well.
2. **Restricted toolset.** Only the tools needed. Check each agent's frontmatter.
3. **Structured output.** Markdown with fixed sections — makes results diffable and reviewable.
4. **No git from any agent.** Even read-only git commands are out. Ramon owns version control through VS Code Source Control.
5. **No source modification from auditor, reviewer, planner, validator, or batch-runner.** Only the consolidator edits `.cs` files, and only after a human-confirmed plan exists.
6. **Strategic decisions stay with Ramon.** The auto-reviewer handles mechanical decisions but escalates anything with design implications.
7. **Resumability over speed.** Long-running batches save state so they survive crashes.
8. **The file on disk is the deliverable.** Inline summaries in chat are not enough.

## Reports & State Directories

```
.claude/reports/
├── audits/         ← tool-auditor output + batch-runner summaries
├── reviews/        ← auto-reviewer output (with Ramon's answers merged on finalization)
├── plans/          ← consolidation-planner output
└── validations/    ← build-validator output

.claude/state/
└── audit-batch-progress.json  ← batch-runner resumability state
```
