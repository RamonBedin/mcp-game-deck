# Feature 21 — Multi-LLM Provider Abstraction

## Status

`proposed` — design pending Ramon approval. Headline feature of v2.1. Companion specs (`21-multi-llm-spec.md` + `21-multi-llm-tasks.md`) will follow when execution starts.

## Problem

MCP Game Deck today is hard-coded to Anthropic Claude via the Claude Agent SDK. Users who:

- Already pay for OpenAI or Google AI subscriptions and want to use their existing credits
- Need a specific model's capability (e.g., Gemini's long context, GPT-4o's vision strengths, smaller/faster models for tedious tasks)
- Work in environments where one provider is blocked or restricted
- Want to A/B test which LLM handles Unity work best for their use case

…have no path. The app is a Claude-only client. This is the single biggest expansion gate for v2.1 adoption — Anthropic-only is a positioning that excludes a large fraction of game-dev professionals who already chose a different provider.

Beyond the "give me a choice" expectation, there's also a tactical advantage: dogfooding multi-LLM helps surface subtle places where the app accidentally assumes Claude-specific behavior (e.g., relying on Claude's particular tool-calling format, or system prompt conventions). Catching these in v2.1 makes the architecture cleaner.

## Proposal

Introduce a `ChatProvider` interface in the supervisor (TypeScript) that abstracts the provider-specific concerns: model invocation, streaming, tool calling, system prompts, message history conversion, token accounting. Three initial implementations:

- **`ClaudeProvider`** — wraps the existing Claude Agent SDK behavior. Default.
- **`OpenAIProvider`** — wraps the OpenAI SDK with chat completions + tools.
- **`GeminiProvider`** — wraps the Google Generative AI SDK with function calling.

UX surface: a provider picker in Settings → Account, with per-provider API key storage in the OS keychain (Tauri Stronghold). A new "Provider" pill on the chat HUD shows which provider is active for the current session; clicking opens the picker. Switching provider mid-conversation starts a fresh session (history doesn't migrate — too many subtle behavioral diffs between providers to roundtrip seamlessly).

**MCP tool calling translation:** Claude Agent SDK speaks MCP natively. OpenAI and Gemini have their own function-calling formats. The provider abstraction layer translates between MCP tool definitions (`tools/list` schema) and provider-specific function signatures, and back-translates function call payloads into MCP `tools/call` invocations on the same Unity-side TCP server. The Unity C# side stays provider-agnostic.

**Subagents:** Claude subagents (the `Plugin~/agents/*.md` specialists) only work with `ClaudeProvider` in v2.1. OpenAI/Gemini providers expose a flag `supportsSubagents: false` and the `Task` tool is removed from their tool list. Multi-provider subagent support is a v2.3+ topic.

**Streaming, cancellation, retry policies:** standardized in the `ChatProvider` interface. Each provider implementation handles its own SDK details internally.

## Scope IN

- **`ChatProvider` interface** in `Server~/src/providers/ChatProvider.ts`:
  - `name`, `id` (e.g., `"claude-sonnet-4-5"`, `"gpt-4o"`, `"gemini-2.0-flash"`)
  - `supportsSubagents: boolean`
  - `streamTurn(messages, tools, systemPrompt, signal) → AsyncIterable<StreamEvent>`
  - `convertTools(mcpTools) → ProviderFunctionSchema[]`
  - `convertToolCall(providerCall) → McpToolCallRequest`
  - `estimateTokens(text) → number`
  - `estimateCost(usage) → CostBreakdown`
- **Three provider implementations:**
  - `ClaudeProvider` (refactor of current `sdk_entry.js` logic)
  - `OpenAIProvider` (uses `openai` Node SDK; supports `gpt-4o`, `gpt-4o-mini`, `o1` models)
  - `GeminiProvider` (uses `@google/generative-ai` SDK; supports `gemini-2.0-flash`, `gemini-2.0-pro`)
- **Provider router:** `Server~/src/providers/router.ts` chooses the right provider per session based on stored config.
- **MCP tool translation:** generic helpers in `Server~/src/providers/tool-translation.ts` that:
  - Convert MCP `tools/list` JSON Schema → OpenAI / Gemini function schemas
  - Convert provider function call payloads → MCP `tools/call` requests
  - Preserve `_meta` annotations as ignored fields (provider-specific consumers don't need them)
- **API key storage:** Tauri Stronghold plugin for per-provider key storage; never in plain config files. Rust commands: `provider_set_api_key(provider, key)`, `provider_test_api_key(provider)`, `provider_clear_api_key(provider)`.
- **Settings UI** — new Settings → Account page:
  - Provider picker (current default)
  - Per-provider API key input (password field with show/hide)
  - "Test connection" button per provider
  - Per-provider model selector (which specific model within the provider family)
- **Chat HUD pill:** small "Provider: Claude Sonnet 4.5" indicator next to the project/Unity pills; clicking opens a quick-switcher.
- **Per-session provider lock:** when a session is created, its provider is locked. Switching provider during an existing session prompts "Start fresh session?" (does NOT migrate history).
- **Token + cost accounting:** for each turn, log token usage (input + output) and estimated USD cost based on per-provider current pricing. Surface in a Stats panel (or simple debug log to start).
- **Subagent capability gating:** OpenAI/Gemini providers expose `supportsSubagents: false`; `Task` tool is hidden from their tool list and `/use-agent` skills error out gracefully.

## Scope OUT (deferred to v2.3+)

- **Local model providers (Ollama, llama.cpp, LM Studio)** — separate feature (F34); architecture allows them but not built-in for v2.1.
- **Enterprise endpoints (Bedrock, Vertex, Azure OpenAI)** — separate feature (F35); same interface, different auth/routing.
- **Provider-aware subagent dispatch** — let OpenAI/Gemini use subagents with provider-specific delegation. v2.3+.
- **Cross-provider session history migration** — too lossy. Lock per session.
- **Tool calling format auto-detection / fallback** — explicit provider config only.
- **Cost budgets / usage caps** — display only, no enforcement.
- **Provider-specific permission models** — single permission system across providers; if a tool fails on Gemini's stricter content filters, that's a per-call error, not a system-level policy.

## Dependencies

- **F19 (Destructive sweep)** — recommended. The MCP `annotations` on tools help the translation layer surface destructive nature to OpenAI/Gemini system prompts ("be cautious with destructive tools") since those SDKs lack a first-class "destructive hint" concept.

## Risks

- **Tool calling fidelity gap** — Claude's MCP tool calling is the most expressive; OpenAI's function calling is similar; Gemini's is less mature with subtle bugs in nested schemas. Translation layer will hit edge cases. Mitigation: keep tool schemas as flat as possible during this rollout; identify which tools break on which provider during spec phase.
- **Streaming format divergence** — each SDK has its own event shape. Standardizing into `StreamEvent` adds an abstraction layer that can leak provider-specific quirks (e.g., OpenAI's `tool_calls` array vs Claude's content blocks). Mitigation: unit tests per provider on a fixed conversation script.
- **API key security** — Stronghold is solid but mistakes happen. Mitigation: never log keys, never include in error messages, mask in DevTools network panel where possible.
- **Cost surprises** — users may not realize GPT-4o input is $X/M tokens. Mitigation: cost breakdown surfaces per turn, plus warnings if a single turn exceeds $0.50 (configurable in v2.3).
- **Subagent disable confusing for OpenAI/Gemini users** — "I delegated to technical-artist and got an error". Mitigation: prominent note in the provider picker; subagent-related UI hides when on non-Claude provider.

## Open questions

1. **Default provider on first install?**
   - Recommendation: Claude (current default). Onboarding can prompt "Choose a provider" but doesn't force.
2. **Per-conversation vs per-session provider selection?**
   - Recommendation: per-session (session = one chat thread). Conversations don't exist as a concept; sessions are the boundary.
3. **Should the Provider HUD pill be clickable to switch live?**
   - Recommendation: yes for "view current", click opens quick-picker. Picking a different provider warns about session reset.
4. **What about Anthropic enterprise endpoints (Bedrock, Vertex)?**
   - Recommendation: deferred to F35 (enterprise endpoints). Same Claude provider impl, different auth path. Don't conflate with v2.1 scope.

## Related notes

This is the v2.1 headline feature and likely to be the biggest single chunk of work in the post-v2.0 roadmap — probably needs to be split into multiple tasks-level features when spec is written:
- F21a: ChatProvider interface + ClaudeProvider refactor (no behavior change)
- F21b: OpenAIProvider implementation
- F21c: GeminiProvider implementation
- F21d: Settings UI + key storage
- F21e: Tool translation layer hardening

Decide split during spec phase. Design above describes the unified target state.
