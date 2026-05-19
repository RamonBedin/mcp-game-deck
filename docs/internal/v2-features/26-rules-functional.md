# Feature 26 — Rules Functional `applies-to` + Auto-Suggestion

## Status

`proposed` — design pending Ramon approval. Picks up F08 (Rules Page) deferrals. Companion specs (`26-rules-functional-spec.md` + `26-rules-functional-tasks.md`) will follow when execution starts.

## Problem

F08 (Rules Page) shipped a working rules system in v2.0 with one big asterisk: the `applies-to` frontmatter field is **informational only**. Users can write a rule with `applies-to: [technical-artist]` expecting it to only inform that subagent, but the v2.0 compiler concatenates all enabled rules into a single bundle regardless of `applies-to`. The field round-trips through the UI (visible, editable, validated) but the bundle compiler ignores it.

Three additional deferrals from F08:

- **No auto-suggestion.** If the user repeatedly corrects the agent on the same point ("don't put scripts in `Assets/`", "don't put scripts in `Assets/`", "DON'T PUT SCRIPTS IN `Assets/`"), there's no detection + "Want to save this as a rule?" prompt. Every user manually transcribes corrections into rules.

- **`chars/4` tokenizer is a rough estimate.** Real tokenizer (e.g., `tiktoken`) would give accurate counts. Not critical but the displayed "~X tokens" is off by 10–30% for non-English / code-heavy content.

- **No `/save-rule` chat skill.** F08 considered and rejected it because captured normative instructions are "too fuzzy to detect reliably". Worth revisiting once auto-suggestion infrastructure exists.

## Proposal

Three layered enhancements.

**(a) Functional `applies-to`.** When a subagent is spawned via `Task(subagent_type=X)`, the supervisor compiles a per-agent rules bundle that includes only rules where:
- `applies-to` is empty (applies to all agents), OR
- `applies-to` contains `X`, OR
- `applies-to` contains a wildcard pattern matching `X` (e.g., `unity-*`)

The per-agent bundle becomes part of the subagent's `systemPrompt.append`. The Claude Agent SDK's mechanism for per-agent system prompts must be researched (F08 noted this is unclear) — likely the subagent definition file's prose body or a per-call SDK option.

**(b) Auto-suggestion.** Background analysis of conversation history detects patterns where the user repeats a corrective instruction within a small window (e.g., 3 times in 5 turns, or 2 times across 2 sessions). When detected, a non-intrusive prompt in the chat: "I noticed you mentioned this several times — save as a rule?" with `[Save] / [Skip]`. Save flow opens the Rules page with a draft pre-populated.

Detection heuristic: NLP-ish but lightweight — keyword overlap + negation patterns + repeated tool-correction patterns. Errs on the side of fewer prompts (suggest only when clearly repeated, not on every minor preference).

**(c) Tokenizer upgrade.** Add `tiktoken-wasm` (or `js-tiktoken`) to the front-end token counter. Use the Claude-equivalent encoder (`cl100k_base` is the standard fallback). Per-rule and total token counts are now accurate to the chunk level.

**(d) `/save-rule` skill.** A new skill `/save-rule <text>` that creates a draft rule with the given text and opens the Rules page in edit mode. Less intrusive than auto-suggestion (user-initiated), useful when the user knows exactly what to add.

## Scope IN

- **Functional `applies-to` filter:**
  - Per-agent bundle compilation in supervisor's `Server~/src/rules-compiler.ts`
  - Wildcard match support (`unity-*` matches `unity-shader-specialist`, `unity-dots-specialist`, etc.)
  - Per-subagent `systemPrompt.append` integration — investigate Claude Agent SDK mechanism in spec phase
  - Empty `applies-to` continues to apply to all (main + all subagents)
- **Auto-suggestion engine:**
  - Conversation-history scanner that runs periodically (every N turns or on idle)
  - Heuristic detection of repeated corrections (configurable thresholds in spec)
  - Chat-inline prompt with `[Save] / [Skip]` actions
  - Skip remembers (don't re-prompt for the same detected pattern for 24h)
- **Tokenizer upgrade:**
  - `js-tiktoken` (lighter than wasm variant) integrated in Rules page token counter
  - Per-rule + total counts shown with new accuracy
  - Old `chars/4` deleted, no fallback
- **`/save-rule` skill:**
  - New skill in `Plugin~/skills/save-rule/SKILL.md`
  - Accepts free-form text as argument
  - Creates rule with auto-generated kebab-case name from first 4–5 words
  - Opens Rules page in edit mode for review

## Scope OUT (deferred to v2.3+)

- **Conditional rules** (only apply if X) — query-language territory, F08 design explicitly avoided.
- **Rule libraries / cross-project sharing** — F31 (Rule libraries) territory.
- **Rule versioning beyond git** — F31 territory.
- **Auto-detect rule conflicts** (rule A says X, rule B says not-X) — too brittle in v2.1.
- **Multi-line `applies-to` editing UI** — keep textarea / array edit; rich editor is over-engineering.
- **Rule effectiveness analytics** — "Rule X has been triggered N times" — F33 (analytics dashboard) territory.
- **Suggestion model upgrade to LLM-based detection** — heuristic suffices for v2.1; LLM detection would be expensive (extra API call per N turns).

## Dependencies

- **F08 (Rules Page)** — shipped in v2.0; F26 enhances it.
- **F13 (Subagent Capabilities)** — enables real subagent execution; `applies-to` functional behavior matters only when subagents are actually doing work.

## Risks

- **Per-agent system prompt mechanism unclear** — Claude Agent SDK may not have a clean per-agent `systemPrompt.append`. Investigation early in spec; fallback is embedding the rules in the subagent's `description` field (less elegant but workable).
- **Auto-suggestion false positives** — annoying prompts that erode trust. Mitigation: conservative thresholds + "Skip and don't ask again" affordance.
- **Tokenizer initialization cost** — `js-tiktoken` adds ~200KB to bundle and ~50ms initialization. Acceptable; cached after first load.
- **`/save-rule` rule naming collisions** — auto-generated kebab-case may collide with existing rules. Mitigation: append a numeric suffix if collision detected.
- **Backward compat with v2.0 rules that have non-empty `applies-to`** — those rules were treated as universal in v2.0. After F26 they become filtered. Mitigation: migration on first v2.1 launch checks for `applies-to` rules and prompts the user to confirm they should now be filtered.

## Open questions

1. **What's the cadence of auto-suggestion analysis?**
   - Recommendation: every N turns (N≈10) plus on `idle` event when user goes 30s without input. Avoids burning compute during active conversation.
2. **Should auto-suggestion also catch *positive* patterns ("always do X")?**
   - Recommendation: yes, but lower precedence. Negative patterns (corrections) are more reliable signal than positive ones.
3. **`/save-rule` argument: free-form text or structured?**
   - Recommendation: free-form. User writes the rule in natural language; the form opens for refinement.

## Related notes

F08 (Rules Page) shipped the foundation. F26 makes rules genuinely powerful by tying them to specific contexts (subagents) and reducing the friction of rule creation (auto-suggestion + save-rule skill).

Implementation surface: supervisor (~rules-compiler), front (auto-suggestion engine + Rules page token counter), new skill file. Estimated 5–7 days. The Claude Agent SDK investigation (per-agent `systemPrompt.append`) is the key unknown.
