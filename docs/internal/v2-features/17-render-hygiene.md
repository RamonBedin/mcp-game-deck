# Feature 17 — Render Hygiene + Performance

## Status

`proposed` — design pending Ramon approval. Companion specs (`17-render-hygiene-spec.md` + `17-render-hygiene-tasks.md`) will follow when execution starts.

## Problem

Three related issues degrade chat UX without breaking functionality outright:

**(a) Duplicate React keys in `MessageView`.** During subagent delegation flows, the console emits `Warning: Encountered two children with the same key, 'toolu_<id>'.`. The same `tool_use_id` ends up in the render list twice — once at the main-turn level, once as a block inside the `Task` tool result. `pairToolBlocks` (or whatever feeds the `MessageView` children list around `ChatRoute.tsx:~398`) doesn't dedupe across that boundary. Symptom is a warning + potential render glitches.

**(b) Slow click handlers.** Chrome DevTools logged `[Violation] 'click' handler took 1047ms` thirteen times during a single validation session. Some handler in the React tree does >1 second of synchronous work on a click. Suspect candidates (none confirmed by profiling yet): tool catalog populating lazily on first interaction, `WorkingStrip` animation setup, a heavy compute on a frequently-fired control.

**(c) `ToolCallGroup` is orphan.** F09 designed a component that collapses 3+ consecutive tool calls of the same category ("Asset / Get Info × 3" instead of three rows) — already built, never wired. It needs grouping logic in `pairToolBlocks` and depends on F10's category metadata.

None of these breaks functionality. All three are signals that the chat surface is accumulating polish debt. Cleaning them up gives a more solid foundation for v2.1.

## Proposal

Three coordinated patches in the chat render layer.

**(a) Dedupe `pairToolBlocks` by `tool_use_id`.** Inspect `App~/src/routes/ChatRoute.tsx::pairToolBlocks` to find where the same ID can emit twice. Add dedup before render — keep the first occurrence (main turn level), drop the subagent-internal duplicate. Validate by repeating a delegation flow and confirming the React warning is gone.

**(b) Profile + fix slow click handler.** Open DevTools Performance tab, reproduce a slow click, identify the slow function in the flame graph. Apply targeted fix: move heavy work to `useEffect` (deferred after paint) or `useMemo` (memoized), convert sync compute to async, or pre-compute on mount instead of on click. Don't change anything until profiling confirms which handler is the culprit — no speculative refactors.

**(c) Wire `ToolCallGroup`.** Implement grouping logic in `pairToolBlocks`: when 3+ consecutive tool calls share the same `category` (from F10 catalog) and there are no text deltas between them, fold them into a `ToolCallGroup` instance. Single-tool sequences render normally. Mixed-category sequences render normally. Verify that grouped state preserves the per-tool result expansion (clicking a grouped row should reveal the individual tool details).

## Scope IN

- **Dedupe fix:**
  - Audit `ChatRoute.tsx::pairToolBlocks` for double-emission paths
  - Add dedup keyed by `tool_use_id` before render
  - Validate via delegation test: open DevTools, run a turn with subagent, confirm no `same key` warning
- **Perf investigation:**
  - DevTools Performance recording during slow click reproduction
  - Identify culprit handler in flame graph
  - Apply targeted fix (memoization / deferred work / async)
  - Validate: 30s of clicking around the app yields zero `>1000ms` violations; target <100ms per click
- **`ToolCallGroup` wiring:**
  - Grouping logic in `pairToolBlocks` (category-based)
  - Verify single-tool and mixed-category sequences still render correctly
  - Verify expansion / per-tool detail access from grouped state
  - Verify accessibility (keyboard nav, screen reader labels)

## Scope OUT (deferred to v2.1+)

- **Virtual scroll of chat history** — at current message volumes, no need. Revisit if a user shows up with 1000+ turn conversations.
- **Animated transitions between grouped/ungrouped state** — static group rendering only; no smooth fold/unfold.
- **Group threshold configuration** — hardcoded 3+ same-category consecutive. No setting.
- **Cross-turn grouping** — groups are within a single turn; turn boundaries always break a group.
- **Manual group expansion preference** — collapsed by default; user can expand. Doesn't remember per-group preference across sessions.
- **Other perf optimizations beyond the identified click handler** — narrow scope to the observed bug.

## Dependencies

- **F10 (Tool Metadata Catalog)** — `ToolCallGroup` wiring needs `category` from the catalog. Must ship after F10.

## Risks

- **Dedup may hide legitimate duplicate calls** — if Claude legitimately calls the same tool twice in a turn (rare but possible), naive `tool_use_id` dedup wouldn't drop either (they have different IDs). But if our dedup logic groups on `tool_use_id`, we're safe — IDs are unique per call. Spec phase verifies the dedup key choice.
- **Perf fix is speculative without profile** — if profiling doesn't immediately reveal the culprit, the spec phase has to acknowledge "needs runtime profile by Ramon" and defer the fix to a follow-up. Don't apply a fix without evidence.
- **`ToolCallGroup` semantics** — what counts as "same category"? `asset-find` and `asset-get-info` both have category `Asset` — group? Probably yes (user thinks of "asset inspection" as one logical step). But `gameobject-create` and `gameobject-delete` (both `GameObject`) probably shouldn't group because they're opposite actions. v2.0 keeps it simple: same category groups. If feedback reveals this is too aggressive, refine to "same category AND same risk tier" in a v2.1 follow-up.
- **Accessibility of groups** — collapsed group must announce to screen readers what's inside. Spec phase verifies a11y story.

## Open questions

1. **Should `pairToolBlocks` dedup also apply within a single agent's blocks (no delegation)?**
   - Recommendation: yes. The fix is general — any duplicate `tool_use_id` in the render list is a bug. Dedup unconditionally.
2. **What if the profiler reveals the slow handler is unfixable (third-party lib)?**
   - Recommendation: document it in the feature wrap-up; skip the fix but note the cause. The violation warning then becomes informational, not actionable.
3. **Should the grouped state show the count clearly ("Asset / Get Info × 3" or just "3 Asset operations")?**
   - Recommendation: count + name of the dominant tool, e.g., `"3 × Asset / Get Info"`. Mockup from F09 has a specific design — follow it.

## Related cycle 2 attempt notes

Cycle 2 surfaced (a) and (b) during validation but didn't address them. (c) was noted in F09 as orphan but never wired. None of the cycle 2 code addresses these directly, so this feature is fresh work from a clean slate. Reference: cycle 2 attempt's `pairToolBlocks` location (`ChatRoute.tsx:~398`) and `ToolCallGroup.tsx` component file (orphan, can be reused as-is or refined during wiring).
