# Feature 19 — Destructive Annotations Sweep

## Status

`proposed` — design pending Ramon approval. Companion specs (`19-destructive-sweep-spec.md` + `19-destructive-sweep-tasks.md`) will follow when execution starts.

## Problem

The MCP specification (2025-03-26) defines tool annotations that clients use to render appropriate UI affordances:

- `destructiveHint: true` — tool may make irreversible changes
- `readOnlyHint: true` — tool only reads data
- `idempotentHint: true` — repeated calls have the same effect as one
- `openWorldHint: true` — tool interacts with external systems (HTTP, network, processes)

The Claude Agent SDK propagates these annotations to the LLM. Tools without annotations land in a permissive "write" tier by heuristic, and the front-end `PermissionRequestCard` shows them with neutral risk styling. **Destructive tools that should clearly require user attention** (delete asset, build player, switch platform, etc.) currently look identical to harmless write tools.

Today, the `[McpTool]` C# attribute carries `RiskTier` (`Read | Write | Destructive`) for the F10 catalog, but **does not** emit MCP-spec annotations on the wire. The 267 tools in the codebase are not classified against the MCP spec axes (which is a separate concern from `RiskTier` — a tool can be `Tier=Read` with `OpenWorldHint=true` if it fetches HTTP, for example).

## Proposal

Extend the `[McpTool]` attribute with three optional boolean hints:

```csharp
[McpTool("asset-delete", Title = "Asset / Delete",
        Tier = RiskTier.Destructive,
        DestructiveHint = true)]
```

`JsonHelper.AppendToolInfo()` emits the annotations under MCP's `annotations` object on `tools/list`. The Claude Agent SDK reads them; the front-end can also derive UI hints (e.g., emphasis for destructive, dim for read-only) — though most of the UI hint logic already comes from F10's `Tier` field.

A **sweep agent** (instructions in `.claude/agents/destructive-sweeper.md`) reads every `Editor/Tools/**/Tool_*.cs` file, classifies the tool against the MCP axes via prose pattern recognition, and proposes annotations. Output: an updated `Editor/.../destructive-sweep-<date>.md` report with tables per domain and confidence flags for ambiguous cases. Ramon reviews the report and applies the annotations either via the agent or manually.

Classification heuristic:
- **`destructiveHint: true`** if the tool deletes files, overwrites assets, builds binaries to disk, switches platforms (irreversible from in-memory state), or modifies external project state in a way that the user can't trivially undo.
- **`readOnlyHint: true`** if the tool only reads — no writes to disk, no state mutation, no external side effects.
- **`idempotentHint: true`** if calling the tool twice with the same args produces the same end-state as calling it once.
- **`openWorldHint: true`** if the tool interacts beyond the Unity project boundary — HTTP fetch (e.g., `unity-docs-manual`), spawning external processes, calling cloud services.

A tool can carry multiple hints (e.g., `unity-docs-manual` is both `readOnlyHint` AND `openWorldHint`).

## Scope IN

- **`[McpTool]` attribute extension:** add `DestructiveHint`, `IdempotentHint`, `OpenWorldHint` as optional `bool?` fields (null = not set).
- **`JsonHelper.AppendToolInfo` emit annotations:** under MCP-spec `annotations` object in `tools/list` response. Only emit fields that are explicitly set (not the null defaults).
- **`.claude/agents/destructive-sweeper.md`** — sweep agent definition with classification heuristic and report format.
- **Sweep run over all `Editor/Tools/**/Tool_*.cs`** — agent reads each file, classifies, proposes annotations. Output: `Editor/.../destructive-sweep-<date>.md` report.
- **Report review + apply pass:** Ramon (or the agent in apply mode) updates `[McpTool]` calls in C# source per the report.
- **Validation (spot-check):**
  - `asset-delete` has `DestructiveHint=true`
  - `batch-execute-menu` and `batch-execute-api` both have `DestructiveHint=true` (can run arbitrary code → user-irreversible side effects)
  - `camera-list` has `ReadOnlyHint=true`
  - `unity-docs-manual` has both `ReadOnlyHint=true` and `OpenWorldHint=true` (HTTP fetch)
  - `reflect-call-method` has `DestructiveHint=true` (can invoke arbitrary code paths)
  - `tests-run` has at minimum `OpenWorldHint=true` if tests can spawn processes; up to the sweep to decide

## Scope OUT (deferred to v2.1+)

- **Automatic hint inference at runtime** — based on which Unity API the C# implementation calls (e.g., `File.Delete` → destructive). Too brittle, false positives. Manual annotations are source of truth.
- **Hint validation at compile time** — no analyzer that enforces "if you call `AssetDatabase.DeleteAsset`, you must set `DestructiveHint=true`". Author's responsibility, sweep catches drift.
- **Per-parameter hints** — e.g., `asset-find` is read-only but accepts a regex parameter that could be DOS'd with catastrophic backtracking. Treated at runtime, not annotation.
- **Sandboxing / actual permission enforcement based on hints** — annotations are advisory only. Tools always run regardless; the user-facing permission card is the gate, not the annotation.
- **Documentation pass on tool descriptions to reflect destructive nature** — leave `[Description]` attribute as-is; the annotation is the structured signal.

## Dependencies

None. F19 is foundational and can ship anytime, but ideally early in the cycle since F10's catalog UI will benefit from the annotations being on the wire by the time it ships.

## Risks

- **Classification ambiguity** — some tools are borderline. Example: `batch-execute-api` is `DestructiveHint=true` because it can run anything, including delete-like ops; but its individual sub-operations may be safe. Sweep agent flags ambiguous cases with `confidence: low` in the report; Ramon reviews.
- **Annotations propagation through SDK** — verify the Claude Agent SDK reads and uses the MCP `annotations` field correctly. If the SDK ignores them, the LLM still sees only the prose description. Spec phase confirms SDK behavior.
- **Sweep agent regression** — if a future tool is added without annotations, it lands in the unannotated default tier. Mitigation: rerun the sweep on each release as part of pre-release checklist.

## Open questions

1. **Should the agent be conservative or aggressive on `DestructiveHint`?**
   - Recommendation: conservative — when in doubt, set `DestructiveHint=true`. Over-warning is annoying but safe; under-warning is dangerous. Sweep report flags reasoning per tool.
2. **Should the sweep agent also annotate `Tier=Destructive` (the F10 catalog field) alongside MCP-spec hints?**
   - Recommendation: yes. They're conceptually distinct but practically correlated. The sweep can set both in a single pass.
3. **Where does the sweep report live in git?**
   - Recommendation: `Editor/.claude/reports/sweeps/destructive-sweep-<YYYYMMDD>.md`. Versioned in the repo so the team can see history. Or under `docs/internal/` if `.claude/` is gitignored.

## Related cycle 2 attempt notes

The cycle 2 attempt did this work and shipped it. The sweep agent ran successfully, classifying ~267 tools across the domain folders. Validation in item 10 of cycle 2 spot-checked four tools and all passed:
- `asset-delete` → `DestructiveHint=true` ✅
- `batch-execute-menu` → `DestructiveHint=true` ✅ (post-sweep adjustment)
- `camera-list` → `ReadOnlyHint=true` ✅
- `unity-docs-manual` → `ReadOnlyHint=true, OpenWorldHint=true` ✅

If the `cycle-2-attempt-1` branch is still kept, **this feature can be a near-pure cherry-pick** — the implementation patches are the lowest-risk reuse of any cycle 2 work. The risk-of-regression on these changes is minimal (purely additive annotations; no behavior change in the tools themselves).

If the branch is gone, the sweep can be rerun from scratch. The agent definition (`.claude/agents/destructive-sweeper.md`) and the report format from cycle 2 are recoverable from this design. Most of the cost was the agent run, not the human review — a few hundred tools take ~10 minutes of agent time and minutes of human review.

**Recommended execution order:** ship F19 early so F10's catalog has annotations on the wire by the time the UI starts consuming category/tier metadata. If F10 ships first, the catalog runs with `Tier` from `[McpTool]` only and adds annotations later — works, but two deploys instead of one.
