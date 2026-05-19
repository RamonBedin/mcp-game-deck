# Feature 10 — Tool Metadata Catalog + UI Integration

## Status

`proposed` — design pending Ramon approval. Companion specs (`10-tool-catalog-spec.md` + `10-tool-catalog-tasks.md`) will follow when execution starts.

## Problem

Today the chat UI shows tools using raw kebab-case names ("Working with asset-delete"), classifies risk via a regex heuristic (`toolTier.ts`), and has no category information. Consequences:

- The `ToolCallNarrativeBlock` produces awkward labels ("Working with asset" for `asset-get-info`) and can't show domain context.
- The `PermissionRequestCard` tier accent comes from a hand-maintained regex that drifts as new tools appear — a destructive tool not matched by the regex shows as "write" instead of "destructive" until someone updates the JS.
- The `AgentCard` has no way to show a sample input per tool.
- The `ToolCallGroup` component is orphan in the codebase — designed in F09 to collapse 3+ consecutive same-family calls, but has no category metadata to group on.

Without an authoritative server-side catalog, every UI consumer reinvents tool classification independently with diverging quality.

## Proposal

Extend `[McpTool]` on the C# side with three new fields — `Tier` (enum `Read | Write | Destructive`), `Category` (string, e.g. "Asset", "Camera"), `ExampleInputs` (optional JSON string with 1–2 sample invocations). Serialize the metadata under `_meta` in the `tools/list` JSON-RPC response (MCP extension namespace; clients that don't understand `_meta` ignore it without breaking).

Front-end exposes a single `toolCatalogStore` (Zustand) populated once at supervisor connect via a new Rust command `list_unity_tools`. A `useToolMeta(name)` hook gives any component the authoritative `{humanLabel, category, riskTier, exampleInputs}` for a tool. The four orphan/heuristic consumers migrate to the hook:

- `ToolCallNarrativeBlock` → uses `humanLabel` ("Asset / Delete") instead of formatted slug
- `PermissionRequestCard` → tier accent from `riskTier`, not regex
- `AgentCard` → sample query from `exampleInputs[0]`
- `ToolCallGroup` → groups consecutive calls by `category`

`toolTier.ts` stays as defensive fallback (returns "write" if a tool somehow isn't in the catalog) but is no longer the source of truth.

## Scope IN

- **C# attribute extension:** `[McpTool]` gains `Tier`, `Category`, `ExampleInputs` fields with sensible defaults (e.g. `Category` derives from folder name during discovery if not set explicitly).
- **Model + discovery propagation:** `McpToolInfo` mirrors the new fields; `ToolDiscovery.cs` fills them; `JsonHelper.AppendToolInfo()` emits them under `_meta`.
- **Manual `Tier=Destructive` sweep** on tools that delete/overwrite irreversibly (asset-delete, script-delete, scene-delete, package-remove, etc — ~15–25 tools expected).
- **Rust command:** `list_unity_tools()` returns `Vec<ToolMeta>` fetched via the supervisor's `tools/list` round-trip on connect.
- **TS types:** `ToolMeta { name, humanLabel, category, riskTier, exampleInputs }` in `ipc/types.ts`.
- **Zustand store:** `toolCatalogStore` with `Map<name, ToolMeta>`, populated on connect, invalidated on disconnect.
- **Hook:** `useToolMeta(name)` returns the meta or undefined.
- **4 UI consumer migrations:** Narrative block, Permission card, AgentCard, ToolCallGroup.
- **Display formatter fix:** tool name display logic must NOT split on hyphen — it should use catalog `humanLabel` directly, OR strip the `mcp__<server>__` prefix and humanize the slug without touching `-` (avoids the "mcp__plan: events_plan_step_marker" rendering bug observed previously).
- **Tool count reconciliation:** runtime catalog count must match the destructive sweep report count (267). If there's drift, identify the missing or extra tool and reconcile (document explicitly if intentional).

## Scope OUT (deferred to v2.1+)

- **Localization of `humanLabel`** — single English string per tool for now.
- **Dynamic tier inference** — sticking with manual `Tier=Destructive` annotations; no automatic detection from method body.
- **Catalog hot-reload** — populated once on connect; supervisor restart required if tools change.
- **Catalog versioning** — no schema version bumps; if the field shape changes, it's a breaking front+back deploy.

## Dependencies

None. F10 is foundational and unblocks F11 (activity stream uses humanLabel), F12 (plan events use category), F17 (ToolCallGroup uses category).

## Risks

- **Drift between sweep report and catalog** — observed during cycle 2 attempt (268 in catalog vs 267 in report). Mitigation: explicit reconciliation task; if discrepancy is intentional (e.g. dev-only tool), document it in the sweep report header.
- **Performance of catalog fetch on connect** — 267 tool entries is small (<100 KB JSON). Negligible unless catalog grows to 1000+.
- **Backward compat with old supervisor builds** — if a supervisor without `_meta` connects, the front falls back to `toolTier.ts` heuristic. Acceptable but worth verifying once.

## Open questions

1. **Default for `Category` when not set?** Folder-name-based ("Asset" from `Editor/Tools/Asset/`) or require explicit annotation per tool?
   - Recommendation: folder-name fallback. Forces zero migration burden for existing 267 tools.
2. **`ExampleInputs` format — single string, JSON object, or structured?**
   - Recommendation: JSON string. Easiest to express in C# `[McpTool]` attribute (which is compile-time constant), front parses on demand.
3. **Should `toolTier.ts` be removed once F10 ships, or kept as fallback?**
   - Recommendation: kept as fallback (returns "write" for unknown tools). Deletion is a F17 follow-up if confidence is high.

## Related cycle 2 attempt notes

The cycle 2 attempt shipped a working tool catalog (item 3 + item 8 passed validation). Two issues surfaced that this feature must resolve cleanly:
- **Tool count drift** (268 vs 267) — included in Scope IN.
- **Display formatter splitting on hyphen** ("mcp__plan: events_plan_step_marker") — included in Scope IN.

The corresponding code on the `cycle-2-attempt-1` reference branch (if Ramon kept it) can be cherry-picked or adapted, but tasks should re-validate everything from scratch given the patch is small.
