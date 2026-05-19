# Feature 13 — Subagent Capabilities (MCP Tools in YAML)

## Status

`proposed` — design pending Ramon approval. Companion specs (`13-subagent-capabilities-spec.md` + `13-subagent-capabilities-tasks.md`) will follow when execution starts.

## Problem

The 10 specialist agents in `Plugin~/agents/*.md` (gameplay-programmer, performance-analyst, qa-lead, systems-designer, technical-artist, unity-addressables-specialist, unity-dots-specialist, unity-shader-specialist, unity-specialist, unity-ui-specialist) declare in their **prose body** which MCP tools they use:

```markdown
## MCP Tools Available

- **Graphics**: `graphics-pipeline-get-info`, `graphics-get-settings`, `graphics-stats-get`
- **Shaders**: `shader-inspect`, `shader-list`
- ...
```

But their **YAML frontmatter `tools:`** field only lists built-ins (`Read, Glob, Grep, Write, Edit, Bash`):

```yaml
---
name: technical-artist
tools: Read, Glob, Grep, Write, Edit, Bash
---
```

The Claude Agent SDK spawns subagents using **only the YAML-declared tools**. When the main Claude delegates to `technical-artist` via `Task(subagent_type="technical-artist")`, the subagent runtime has zero MCP access. It hits Bash approval gates trying to do work that the prose tells it to do via MCP — and truncates partway through.

Observed during a cycle 2 validation attempt: delegated `technical-artist` to analyze rendering setup of an active scene. Agent invoked, tried Bash to gather info, got approval-gated immediately, never managed to call `graphics-pipeline-get-info` (which the prose says is part of its toolkit), eventually returned a partial result. Main Claude had to redo the work from scratch using MCP directly.

This breaks **delegation as a feature**. With 10 specialist agents that can't use MCP, the whole specialization layer is decorative.

## Proposal

Cross-reference the prose body of each specialist against the runtime tool catalog. Add `mcp__game-deck__<tool>` entries to the YAML `tools:` field for every MCP tool referenced in the prose. Preserve built-ins.

**Critical hygiene:** the prose itself contains *stale tool names* that don't match the runtime catalog (the cycle 2 attempt found 13 distinct renames affecting 9 agents — `vfx-list-particle-systems` should be `vfx-list-particles`, `unitydocs-get-doc` should be `unity-docs-get`, `build-project` should be `build-player`, etc). If only the YAML is fixed and the prose keeps stale names, the LLM consults its own prose to decide which tool to call, picks the stale name, and fails. **Both YAML AND prose must be aligned with the authoritative catalog.**

The authoritative source for tool names is the runtime catalog (`list_unity_tools` invoke) or — if F19 is already shipped — the destructive sweep report at `.claude/reports/sweeps/destructive-sweep-*.md` which lists every tool by domain with exact names.

## Scope IN

- **Audit pass:** for each of the 10 specialists, read the `## MCP Tools Available` prose section, list every tool name mentioned.
- **Cross-check against catalog:** confirm each name exists. Flag stale names and decide a 1:1 mapping to the real name (or 1:N if a stale name has multiple plausible real equivalents — e.g., `batch-execute` could map to `batch-execute-api` or `batch-execute-menu`; pick based on prose semantics).
- **YAML update:** append `mcp__game-deck__<real-name>` entries to `tools:` for each specialist. Preserve built-ins. Preserve order from prose (semantic grouping by domain).
- **Prose update:** rewrite stale names inline so the LLM's own context shows the real names.
- **`systems-designer.md`:** if its prose has no `## MCP Tools Available` section (pure design specialist, no MCP needs), leave it unchanged. Document the exception.
- **Validation:** delegation test from the chat: `"Use the technical-artist agent to analyze the rendering setup of the active scene"`. Expected outcome:
  - Agent completes end-to-end on its own
  - WorkingStrip (when F11 is also shipped) shows `"Delegating to technical-artist"` + `"TA"` avatar
  - No Bash approval gates, no main-Claude takeover

## Scope OUT (deferred to v2.1+)

- **Dynamic tool discovery for subagents** — keeping the YAML `tools:` field static; if new MCP tools are added later, the specialists' YAML needs to be updated manually.
- **Per-domain tool inheritance** — e.g., a "rendering" base spec that `technical-artist` and `unity-shader-specialist` both inherit from. v2.1 maintenance if it becomes painful.
- **Convert `tools:` to YAML block list format** — comma-separated remains for now (some specialists end up with 47-entry lines which is ugly, but converting is a separate cleanup pass).
- **`provider:` frontmatter for multi-LLM** — F21 (Multi-LLM) territory; each subagent will eventually be able to use a specific provider, but not in v2.0.
- **Validation against `canUseTool` callback** — if the runtime callback filters more aggressively than the YAML, this feature can't fix that. If observed during validation, escalate to a follow-up KI; the spec narrows to YAML+prose alignment only.

## Dependencies

None. F13 is independent and can ship at any time. Recommended early in the cycle because it unblocks delegation testing for downstream features (F11 sub-test B, F12 subagent-delegated plan execution).

## Risks

- **`canUseTool` is the real filter** — even after YAML is correct, the supervisor's `canUseTool` callback may filter MCP tool calls based on a separate policy. If validation fails after F13 ships, the next investigation moves to that callback (likely a quick follow-up patch). Mitigation: spec phase verifies `canUseTool` defaults are permissive for `mcp__game-deck__*` tools.
- **Stale name proliferation** — prose body of each specialist may have references in spots beyond the `## MCP Tools Available` section (e.g., examples deeper in the body). Spec phase does a full-text scan of each agent file, not just the dedicated section.
- **Comma-separated line length** — `unity-specialist.md` ends up with ~47 MCP tools. Single line of comma-separated values is hard to maintain. Acceptable trade-off for v2.0 (avoids YAML block-list structural change). Cleanup pass possible later.
- **Tool name drift over time** — if catalog tool names change (rename pass during F19 destructive sweep), the YAML/prose pair drifts again. Mitigation: F19 must include a sweep over agent YAMLs whenever tool names rename.

## Open questions

1. **`camera-get` in the prose maps to `camera-list` or `camera-get-brain-status` (or both)?**
   - From cycle 2 investigation: `camera-list` matches "visual validation" semantics better. Brain-status is Cinemachine-specific. Recommendation: `camera-list` 1:1. If Cinemachine inspection is needed later, add brain-status as a follow-up.
2. **`batch-execute` → `batch-execute-api` or `batch-execute-menu` (or both)?**
   - From cycle 2 investigation: prose mentions "set up multiple GameObjects" / "multiple operations in one call" — API semantics. Recommendation: `batch-execute-api` 1:1.
3. **Should the audit also harvest tool names from skill files (`Plugin~/skills/*.md`), not just agents?**
   - Recommendation: not in v2.0. Skills are usually short and have less surface for stale tool names. If a skill is observed misbehaving, fix it then.

## Related cycle 2 attempt notes

The cycle 2 attempt did exactly this work — YAML+prose audit, cross-check against catalog, 13 renames identified, 9 agents updated, ~37 prose corrections, 157 MCP tool entries added. The implementation patches are in the `cycle-2-attempt-1` branch and **can be cherry-picked directly** if Ramon kept the branch — this is the lowest-risk reapplication of any cycle 2 work.

If the branch isn't kept, the patches are recoverable from this design + the cross-check report that would be generated again in spec phase.
