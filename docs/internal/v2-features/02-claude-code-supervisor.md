# Feature 02 — Claude Code Supervisor

> **Note on numbering:** the original Feature 02 was "Orchestrator Agent" — superseded by ADR-001 and deleted. This spec reuses slot 02 for the work that ADR-001 actually requires: spawning Claude Code as the chat engine inside the Tauri app.

## Status

`agreed` — design follows directly from `ADR-001-claude-code-sdk-as-engine.md`. **Re-validated 2026-04-29 after F07 merged into v2.0** (see notes inline below where F07 work changed the context). Ready to generate `02-claude-code-supervisor-spec.md` (executable spec) and `02-claude-code-supervisor-tasks.md` (decomposed tasks for Claude Code) once Ramon approves the locked decisions below.

## Problem

Feature 01 shipped a Tauri scaffold (React + Rust supervisor + TCP client + chat UI) wired to an echo stub on the Node side. The chat round-trip works, but the brain inside Node is a placeholder. ADR-001 locked that the brain must be Claude Code itself — spawned by Tauri via `@anthropic-ai/claude-agent-sdk`. This feature implements that engine swap.

When this feature ships:
- The user opens the Tauri app from the Editor pin
- The app detects that Claude Code is installed and authenticated
- The Tauri Node child spawns the Agent SDK pointing at the user's Unity project root
- The SDK loads the bundled `Plugin~/` (skills + 10 Unity specialist agents) via the `plugins` option, and reaches the C# MCP Server through `mcp-proxy.js`
- The user types in chat, Claude Code answers, tool calls roundtrip through Unity, attachments work, sessions persist

## Proposal

The work breaks into six concerns. Each is small enough to ship independently:

1. **Auth and install detection** — first-run flow detects whether `claude` is on PATH and authenticated; surfaces a clear "log in first" CTA when not
2. **Engine spawn** — Node child uses `@anthropic-ai/claude-agent-sdk`'s `query()` to spawn `claude` with the right flags, env vars, working directory
3. **Asset surfacing** — `Plugin~/` (the bundled Claude Code plugin: skills under `Plugin~/skills/<name>/SKILL.md`, agents under `Plugin~/agents/<name>.md`, manifest at `Plugin~/.claude-plugin/plugin.json`) surfaced via the SDK's `plugins: [{type:"local", path}]` option — NOT via `--add-dir` or copy-to-`.claude/`. **Updated 2026-04-30 after F02 task 3.1 empirical pivot:** original plan was `--add-dir` for skills + copy step for agents (per ADR-001 §"Validations: status" item 3 option d), but the SDK's `additionalDirectories` does not trigger skill discovery (only filesystem read access). The plugin mechanism is the official, supported path for distributing both skills and agents in a single bundle, with namespace `mcp-game-deck:<name>`.
4. **Wire protocol migration** — Tauri ↔ Node messaging shifts from the custom JSON-RPC dialect to the Agent SDK's native message stream; React side absorbs the change in `App~/src/ipc/`
5. **Attachment migration** — the React drag-drop flow stops building base64 content blocks and starts sending file paths the way the SDK expects
6. **Lifecycle** — supervisor restart, crash detection, clean shutdown, port collision recovery (already partially handled by Feature 01 infrastructure)

## Scope IN

- First-run UX detecting Claude Code install + auth status (per ADR-001 §"Validations: status" item 6)
- Node-side replacement of the echo stub with `@anthropic-ai/claude-agent-sdk` `query()` loop
- Spawn `claude` subprocess with: working directory = user's Unity project root, `plugins: [{type:"local", path: <package>/Plugin~/}]` surfacing both skills and agents in a single bundle, MCP config pointing at `mcp-proxy.js`. `additionalDirectories` reserved for `<unity-project>/ProjectSettings/GameDeck/commands/` (filesystem read access only, not discovery)
- **(Obsolete after F02 task 3.1 pivot)** ~~Copy + `{{KB_PATH}}`-resolve `Agents~/<n>.md` → `<unity-project>/.claude/agents/gamedeck-<n>.md` on first launch (and on `claude` version bump or package version bump)~~ — plugin mechanism replaces this; agents now live at `Plugin~/agents/<n>.md` inside the package itself and are surfaced by the SDK without a copy step
- **(Obsolete after F02 task 3.1 pivot)** ~~Track copied agents in a manifest (e.g., `<unity-project>/Library/GameDeck/installed-agents.json`) so uninstall is clean~~ — nothing to track when nothing is copied; uninstall handled by UPM/git removing the package itself
- Migrate React's chat round-trip from echo-stub-over-JSON-RPC to Agent SDK message stream
- Migrate attachment handling from base64 wire payloads to file paths (per ADR-001 §"Validations: status" item 7)
- Surface Claude Code's permission mode in the Tauri React UI (`default` / `acceptEdits` / `plan` / `bypassPermissions` / `auto`); read/write via the SDK
- Surface session list (Claude Code's own sessions) in the Tauri UI as a navigation aid; drop the custom `.game-deck-sessions.json` from Feature 01 design
- Health check on supervisor startup: spawn, ping, confirm, surface failure cleanly
- Pin a known-good Claude Code version range in `package.json` and detect drift on launch (warn but don't block)
- Windows-specific subprocess hygiene per ADR-001 §"Validations: status" item 4 (CREATE_NEW_PROCESS_GROUP equivalent in Node, UTF-8 encoding, stdin handling)
- Error path: if `claude` isn't installed, surface a one-click link to install docs and explain what's missing; if authenticated but Claude API down, surface "Claude API unavailable, retry"

## Scope OUT (deferred)

- **Subagents from `Agents~/` reaching MCP Game Deck tools** — ADR-001 §"Validations: status" item 5 documented this as a known Claude Code bug (Issue #25200 et al.). **As of 2026-04-29 the bug appears fixed** — release notes confirm "Fixed MCP tools not available to subagents" and the official subagent docs now state "Subagents inherit all tools from the main conversation, including MCP tools." Decision #3 below is updated accordingly: agents stay as agents in v2.0, with empirical validation during Group 3 implementation. The previously-planned "Feature 02b — Specialists as skills" follow-up is removed from the roadmap (no longer needed)
- **Multi-modal beyond images and PDFs** — video, audio attachments. v2.x territory
- **Session search / cross-session memory UI** — Claude Code provides session resumption; surfacing it in our React UI is enough for v2.0
- **Custom system prompt overlay from Rules tab** — Feature 08 (Rules Page) owns that; Feature 02 only exposes the `--append-system-prompt` plumbing the Rules tab will use
- **Plan mode interactive `ask_user`** — Feature 04 owns the in-process tool registration via `@tool` decorator; Feature 02 only ensures the SDK is wired such that Feature 04 can plug in
- **`/save-plan` and `/plan-execute`** slash commands as skills — Feature 06 (Plans CRUD) owns those skills
- **Spec-Kit, MCP composition, custom user `CLAUDE.md`** — these all just work because Claude Code reads them natively. No code in this feature
- **Linux / macOS support** — Windows-first for v2.0 (matches Feature 01 + Feature 07 scope). Cross-platform comes after
- **CI / headless mode** — running the Tauri app non-interactively. v2.x

## Dependencies

- **Feature 01 (External app)** — done. The supervisor pattern, JSON-RPC scaffold, React shell, TCP client all stay; this feature swaps the brain.
- **Feature 07 (Editor pin)** — **done as of 2026-04-29.** F07 launches the Tauri app via `Process.Start` with the env-var contract that F02 consumes (`UNITY_PROJECT_PATH` becomes the supervisor's `cwd`; `UNITY_MCP_HOST`/`UNITY_MCP_PORT` feed the MCP proxy config). F02 must NOT change the env-var contract — only consume it. Note the existing post-ADR-001 debt visible after merging F07: running `pnpm tauri dev` prints `Cannot find module 'agent-sdk-stub.js'` in the terminal because the Rust supervisor still spawns the deleted stub. **F02 Group 2 fixes this directly** (replacing the stub with the real Agent SDK invocation).
- **`@anthropic-ai/claude-agent-sdk` license check** — ADR-001 §"Validations: status" item 1 must be answered before public release (not before implementation start)
- **Claude Code authentication onboarding research** — ADR-001 §"Validations: status" item 6, **resolved**: detect-only, no credential handling in our UI
- **`{{KB_PATH}}` resolution decision** — ADR-001 §"Validations: status" item 3, **decision-required**: option (d) recommended. Lock it before tasks group 3 (asset surfacing).
- **Subagent MCP tool bug** — ADR-001 §"Validations: status" item 5 originally **resolved KNOWN BUG** with provisional workaround. **Re-checked 2026-04-29:** Anthropic appears to have fixed it (release notes: "Fixed MCP tools not available to subagents"; official subagent docs now state subagents inherit MCP tools). Decision #3 updated accordingly. Empirical validation still required during Group 3 implementation.
- **Multi-modal attachment local test** — ADR-001 §"Validations: status" item 7, **needs-local-test**: do during tasks group 5.

This feature consumes F07's launch contract (env vars) but is otherwise independent — the pin gives the user an entry point; the supervisor gives the entry point a working backend.

## Locked decisions (initial set; expand as design firms up)

### Decision #1 — Engine: `@anthropic-ai/claude-agent-sdk`, not `claude -p` direct, both as external dependencies

The Tauri Node child uses the **Agent SDK's `query()` loop** to drive Claude Code. Two reasons:

- The SDK handles permission surfaces, message streaming, session management, attachment encoding, tool result parsing — all of which we'd reimplement otherwise.
- The SDK absorbs most of the platform quirks documented in ADR-001 §"Validations: status" item 4 (Windows stdin, raw-mode, etc.). Direct `claude -p` invocation hits those bugs.

**Both the SDK and Claude Code are external dependencies** — NOT bundled in the MSI. Per ADR-001 §"Validations: status" item 1, the Anthropic Commercial Terms do not explicitly grant redistribution rights for these proprietary packages. The supervisor:

- Detects Claude Code on PATH at first launch; if missing, surfaces a clear install CTA in the React UI pointing at Anthropic's official install docs (`curl install.sh`, Homebrew, WinGet)
- Detects the `@anthropic-ai/claude-agent-sdk` package in its managed Node runtime; if missing, runs `npm install @anthropic-ai/claude-agent-sdk` on first launch (one-time cost) into a Tauri-app-managed location (e.g., `App~/runtime/node_modules/`, decided at task time)

Trade-off: tighter coupling to the SDK's API surface. Mitigation: the SDK is from Anthropic, semver'd, and the only "client" Anthropic explicitly supports for embedded use cases.

### Decision #2 — Asset surfacing: skills + agents via SDK `plugins` option, no copy step

**Updated 2026-04-30 after F02 task 3.1 empirical pivot.** Original ADR-001 §"Validations: status" item 2 (skills via `--add-dir`, agents via copy) was wrong about the SDK behaviour: `additionalDirectories` only grants filesystem read access, NOT skill auto-discovery. The Claude Agent SDK's `plugins: [{type:"local", path}]` option is the official mechanism for distributing both skills and agents as a bundled, versioned plugin.

The package's `Plugin~/` directory is structured as a Claude Code plugin per the official manifest convention:

- `Plugin~/.claude-plugin/plugin.json` — manifest (name=mcp-game-deck, version, license, etc.)
- `Plugin~/skills/<n>/SKILL.md` — 22 generic skills, namespaced as `mcp-game-deck:<n>`
- `Plugin~/agents/<n>.md` — 10 Unity specialist agents, invoked via `@agent-mcp-game-deck:<n>`

The supervisor passes `Plugin~/` to the SDK via `plugins: [{type:"local", path}]`. NO copy step into `<unity-project>/.claude/`. NO manifest tracking. NO uninstall menu — when the user removes the UPM package, the plugin disappears with it.

For `<unity-project>/ProjectSettings/GameDeck/commands/` (opt-in user-authored commands, separate from skills), the supervisor passes the path via `additionalDirectories` (filesystem read access; commands aren't a discovery-driven mechanism in modern Claude Code anyway — they're a legacy format).

Skills get `plugins: [...]` registration. `additionalDirectories` reserved for commands path.

### Decision #3 — 10 specialists ship as agents (subagent MCP bug appears fixed)

**Updated 2026-04-30.** Original ADR-001 §"Validations: status" item 5 documented Issue #25200 (custom subagents in `.claude/agents/` couldn't reliably call MCP tools) and recommended converting the 10 specialists into skills as a workaround. **Re-checking the issue and Claude Code release notes on 2026-04-29:**

- Official subagent docs now state: *"Subagents can use any of Claude Code's internal tools. By default, subagents inherit all tools from the main conversation, including MCP tools."*
- ClaudeLog release notes (April 2026) include: *"Fixed MCP tools not available to subagents."*
- Other relevant fixes shipped: *"`--print` mode now honors the agent's `tools:` and `disallowedTools:` frontmatter"*; *"Agent frontmatter `mcpServers` are now loaded for main-thread agent sessions via `--agent`."*

The specialists ship as **agents** (their original design), surfaced via the `plugins` option of Decision #2 (the consolidated `Plugin~/agents/` directory inside the bundled plugin). They keep context isolation. Group 3 of implementation includes an empirical smoke test — spin up `gamedeck-unity-shader-specialist` via `Task` tool, ask it to call an MCP Game Deck tool, confirm it works. If the smoke test fails despite the documented fix, fall back to the skills-based plan from the original Decision #3 (rewrite the 10 markdown files into `Plugin~/skills/` with adjusted YAML frontmatter). Tasks plan accordingly with the skills fallback called out.

### Decision #4 — `{{KB_PATH}}`: contextual after plugin pivot

**Updated 2026-04-30 after F02 task 3.1 empirical pivot.** Original plan from ADR-001 §"Validations: status" item 3 option (d) was "substitute `{{KB_PATH}}` placeholders at copy time, when copying `Agents~/<n>.md` → `<unity-project>/.claude/agents/gamedeck-<n>.md`". After the pivot, **there is no copy step** — agents live inside `Plugin~/agents/<n>.md` in the package itself and are versioned with the rest of the plugin.

This means `{{KB_PATH}}` substitution is only relevant if an agent text references the Knowledge Base via an absolute path. With the plugin layout:

- If `KnowledgeBase~/` (if present) lives inside the plugin or at a known relative location, agents can reference it via paths relative to the plugin root — no substitution needed.
- If knowledge base content is intentionally absolute (e.g., the user's home directory), `{{KB_PATH}}` substitution can happen at first-launch time directly into `Plugin~/agents/<n>.md` (one-time, in-place) OR be resolved by the plugin's own logic at runtime (via env var read inside the agent prompt).

The original copy-time substitution machinery is unnecessary. Specific resolution path is decided at task 3.2 time when consolidating `Agents~/` into `Plugin~/agents/`.

### Decision #5 — Permission mode UI: thin wrapper over Claude Code

The React UI exposes a permission mode toggle (Tauri command → SDK config → Claude Code), but the actual permission decisions happen inside Claude Code. We don't reimplement the permission resolver. ADR-001 §"Mapping `Server~/src/index.ts` to Claude Code equivalents" already locked this; this decision just confirms the implementation surface in Feature 02.

The UI surfaces the five modes Claude Code supports: `default`, `acceptEdits`, `plan`, `bypassPermissions`, `auto`. Selecting `plan` from the React UI translates to `permissionMode: "plan"` in the SDK call. Shift+Tab toggling inside the chat is also wired.

### Decision #6 — Session storage: Claude Code's, not ours

Drop the custom `.game-deck-sessions.json` and the 100-session / 30-day TTL machinery. Claude Code has its own session storage with `resume: sessionId`. The Tauri React UI exposes a session list by reading from the SDK; we don't persist or back up sessions ourselves.

Trade-off: less control over retention. Acceptable — Claude Code's defaults are reasonable, and any user with strong feelings can configure them in their own Claude Code setup.

### Decision #7 — Windows subprocess hygiene baked into the supervisor

Per ADR-001 §"Validations: status" item 4, the supervisor on Windows:

- Spawns the Node child with the appropriate process group flag (Node equivalent of `CREATE_NEW_PROCESS_GROUP` — `detached: true` + `stdio: 'ignore'` for the parts we don't manage explicitly, or `windowsHide: true` plus careful signal handling)
- Sets `encoding: 'utf-8'` on all stdio streams
- Passes long system prompts via `--append-system-prompt` (file-based) rather than as CLI args
- Normalizes attachment paths to forward slashes before sending to the SDK (in case the SDK is sensitive)
- Rejects nested `claude -p` invocation patterns (we don't do this, but the supervisor's API surface should make it impossible to accidentally start)

## Implementation phases (high level — refined into tasks at spec-time)

### Group 1 — Auth and install detection
- Detect `claude` on PATH (Windows: `where.exe claude`, fallback to checking common install locations)
- Detect `@anthropic-ai/claude-agent-sdk` in the supervisor's managed Node runtime; if missing, run `npm install @anthropic-ai/claude-agent-sdk` on first launch into a managed location (per Decision #1 + ADR-001 §"Validations: status" item 1: SDK is NOT bundled in the MSI)
- Detect Claude Code auth (run `claude /status` non-interactively, parse output)
- React first-run UI: panel showing what's missing (Claude Code not installed / not logged in / SDK installing) with appropriate CTAs
- Tauri command `check_claude_install_status()` returning `{ claudeInstalled, claudeAuthenticated, sdkInstalled, claudeVersion }`

### Group 2 — Engine spawn
- Replace `agent-sdk-stub.js`-style stub with real `@anthropic-ai/claude-agent-sdk` `query()` invocation
- Pass cwd = user's Unity project root (resolved via `UNITY_PROJECT_PATH` env var injected by Feature 07's pin)
- Wire MCP config pointing at `Server~/dist/mcp-proxy.js` with `UNITY_MCP_PORT` and `UNITY_MCP_HOST` envs
- Smoke test: send "echo test" prompt, confirm Claude Code responds with text (not stub echo)

### Group 3 — Asset surfacing
- **Updated 2026-04-30 after task 3.1 empirical pivot.** Plugin-based approach replaces the original copy-step plan (per Decision #2 above):
- Surface `<package>/Plugin~/` to the SDK via `plugins: [{type:"local", path}]`. Plugin contains `Plugin~/.claude-plugin/plugin.json` (manifest), `Plugin~/skills/<n>/SKILL.md` (22 skills), and `Plugin~/agents/<n>.md` (10 specialists). Skills appear as `mcp-game-deck:<n>`, agents as `@agent-mcp-game-deck:<n>`.
- Consolidate `Agents~/` into `Plugin~/agents/` (task 3.2). After consolidation, the standalone `Agents~/` directory is deleted.
- `{{KB_PATH}}` substitution simplified per Decision #4 — resolved by plugin layout convention or one-time substitution at task 3.2 time, NOT at runtime.
- ~~Manifest tracking~~ removed — nothing copied = nothing to track.
- ~~Re-run on package version bump~~ removed — plugin updates with the package automatically (UPM/git).
- Empirical smoke test in task 3.4: invoke an agent via `@agent-mcp-game-deck:<n>` and confirm MCP tool call succeeds (validates Decision #3).
- Verify in `claude` interactively or in the Tauri chat: "list skills" returns 22 with `mcp-game-deck:` namespace + 10 built-ins (validated empirically during task 3.1)

### Group 4 — Wire protocol migration
- Replace `App~/src/ipc/types.ts` and `App~/src-tauri/src/node_supervisor/jsonrpc.rs` with Agent SDK message types
- React side: `conversationStore` consumes streamed messages from the SDK (text deltas, tool use blocks, tool results) instead of monolithic responses
- Permission mode UI hooks into SDK config

### Group 5 — Attachment migration
- React drag-drop hands file paths to Tauri (already does this)
- Tauri sends paths to the SDK (replaces base64 encoding)
- Test with: small PNG, multi-MB PDF, Windows path with spaces, Windows path with non-ASCII chars (per ADR-001 §"Validations: status" item 7)
- Document findings in the supervisor module

### Group 6 — Lifecycle and resilience
- Supervisor restart cleans up `claude` subprocess too (currently only stops Node child)
- Health check on startup: spawn, send a minimal `query()`, confirm response within timeout
- Crash detection emits `node-sdk-status-changed: crashed` event the React UI already listens for
- Pin a Claude Code version range in `package.json`; warn-but-don't-block if user has a different version

## Cost estimate

**Medium.** Bigger than Feature 04 / 06 / 08 (each small revisions of single-screen UI); smaller than Feature 01 (which built the entire Tauri scaffold). Most of the wiring exists from Feature 01; this feature replaces brains, not arms.

Group 1 + 2 are the critical path — once they work, the user can have a real conversation. Groups 3 + 4 are the polish that makes it feel like the product. Groups 5 + 6 are reliability.

## Definition of done

- User clicks the Editor pin (F07, already shipped) → Tauri app opens → first-run flow detects Claude Code is installed and logged in (or surfaces a clear next step)
- User types in chat → Claude Code answers using the 10 Unity specialists (as agents, per decision #3) plus the 22 generic skills, plus any user-configured MCPs and Spec-Kit
- User asks Claude Code to do something in Unity → tool call roundtrips through `mcp-proxy.js` → C# MCP Server → reply back, no stalls
- User drags a PNG into the chat → image is sent as attachment → Claude Code analyzes it
- User toggles permission mode in the React UI → Claude Code respects it
- User closes the Tauri window (or Unity Editor) → `claude` subprocess and Node child both terminate cleanly within 2s; no zombies
- User uninstalls the package — UPM/git removes the package directory; the plugin (and all its skills + agents) disappears automatically. No Tauri menu action needed; nothing was ever copied into the user's Unity project.
- The pre-existing `Cannot find module 'agent-sdk-stub.js'` errors that F07 left behind are gone (replaced by real Agent SDK invocation)
- Smoke test on Windows 11: open app via the F07 pin, run a 3-turn conversation including a tool call and an attachment, close cleanly. Repeat 5 times. No crashes, no auth re-prompts, no orphan processes.

## After Feature 02

The supervisor is the spine. Once it works:

- **Feature 04 (Interactive Plan Mode)** can register `ask_user` as an in-process tool via the SDK's `@tool` decorator
- **Feature 05 (Permission System)** is mostly already done — surface and validate
- **Feature 06 (Plans CRUD)** can ship `/save-plan` and `/plan-execute` as skills in `Skills~/`
- **Feature 08 (Rules Page)** can inject rules via `--append-system-prompt`

(The previously-planned "Feature 02b — Specialists as skills" is removed: with the subagent MCP bug fixed, specialists ship as agents in v2.0 directly. If the empirical smoke test in Group 3 finds the fix incomplete, the rewrite-to-skills plan moves into a normal F02 task instead of a separate feature.)

## References

- `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md` — the parent decision; this feature implements it
- `docs/internal/v2-architecture.md` — process layout, communication channels (already updated for ADR-001)
- `docs/internal/v2-features/01-external-app.md` — predecessor (delivered scaffold)
- `docs/internal/v2-features/04-interactive-plan-mode.md` — depends on this feature for SDK plumbing
- `docs/internal/v2-features/05-permission-system-fix.md` — depends on this feature for permission UI surface
- `docs/internal/v2-features/06-plans-crud.md` — depends on this feature for slash command plumbing
- `docs/internal/v2-features/08-rules-page.md` — depends on this feature for system prompt injection
- Claude Agent SDK docs: https://platform.claude.com/docs/en/agent-sdk/overview
- Claude Code subagent docs (read for asset surfacing): https://code.claude.com/docs/en/sub-agents
- Claude Code skills docs: https://code.claude.com/docs/en/skills
- Claude Code authentication docs: https://code.claude.com/docs/en/authentication
