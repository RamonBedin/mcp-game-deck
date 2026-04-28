# `docs/internal/`

Internal design docs and roadmap for MCP Game Deck. **Not user-facing** — these are working documents for Ramon (and any future contributors) to align on architecture before code is written.

## Structure

```
docs/internal/
├── README.md              ← you are here
├── roadmap.md             ← v1.2 / v2.0 / v2.1 milestones, what goes where
├── v2-architecture.md     ← cross-cutting architectural decisions for v2.0
├── v2-features/           ← one doc per feature, structured the same way
│   ├── 01-external-app.md
│   ├── 02-orchestrator-agent.md
│   ├── 03-slash-commands.md
│   ├── 04-interactive-plan-mode.md
│   ├── 05-permission-system-fix.md
│   ├── 06-plans-crud.md
│   ├── 07-editor-status-pin.md
│   ├── 08-rules-page.md
│   └── 09-design-handoff.md
└── decisions/             ← architectural decision records (ADRs) when made
```

## Conventions

**Feature doc structure** — every file in `v2-features/` follows this:

1. **Status** — draft / agreed / in-progress / done
2. **Problem** — observable symptom in current product
3. **Proposal** — concrete change
4. **Scope IN / OUT** — what's covered, what's deferred
5. **Dependencies** — which other features must land first
6. **Cost estimate** — small / medium / large with rationale
7. **Risks** — what could go wrong
8. **Milestone** — v1.2 / v2.0 / v2.1
9. **Open questions** — unresolved design questions

**ADR convention** — when a cross-cutting decision is made (e.g. "we picked Tauri over Electron"), write a short note in `decisions/` named `NNN-short-title.md`. Even one paragraph is fine. The point is recording WHY a choice was made so we don't re-debate it later.

## Living docs

These files are versioned in git but evolve as we learn. Don't treat them as frozen specs — when reality differs from the doc, **update the doc**. Out-of-date design docs are worse than no docs.

When a feature ships, mark its status `done` and link to the actual implementation. Don't delete the doc — historical context is useful.

## Not user-facing

User-facing docs (README, install guide, tool reference) live elsewhere (root `README.md`, eventually a `docs/user/` if needed). Anything in `docs/internal/` is meant for the people building the tool, not the people using it.
