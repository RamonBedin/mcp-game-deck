# Feature 34 — Local Model Providers (Ollama, llama.cpp, LM Studio)

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

Three groups of users want fully-local LLM execution:

- **Privacy-sensitive teams** — studios working on unreleased IP, NDA contractors, anyone whose code can't legally leave the local network. Cloud LLM use is a non-starter regardless of provider's data policy.
- **Offline / disconnected users** — game-dev contractors traveling, working in low-connectivity environments, or just preferring to disconnect during deep work. Cloud LLMs are useless without reliable internet.
- **Cost-sensitive heavy users** — high-volume users for whom even Anthropic's Sonnet pricing accumulates faster than they prefer. Local execution shifts cost to one-time hardware investment.

The F21 (Multi-LLM) architecture explicitly supports adding provider implementations. F34 adds the local options.

## Proposal

Implement `LocalProvider` family under F21's `ChatProvider` interface. Three initial adapters:

- **`OllamaProvider`** — connects to a local Ollama server (default `http://localhost:11434`). Selectable model from the user's locally installed models (queries Ollama's `/api/tags` endpoint).
- **`LlamaCppProvider`** — connects to `llama.cpp` running in server mode (default `http://localhost:8080`). User specifies the model path and serving args in app settings.
- **`LMStudioProvider`** — connects to LM Studio's local server (OpenAI-compatible API at `http://localhost:1234/v1`). Selectable model from LM Studio's loaded models.

UX surface: Settings → Account provider picker (from F21) gains a "Local" section listing the three adapters. Per-adapter config (endpoint URL, default model). Connection test button verifies the local server is up before saving.

**Tool calling caveat:** local models with reliable function calling are limited. Ollama models like `qwen2.5-coder:32b` and `llama3.1:70b-instruct` have some function calling, but quality varies. v2.3 documents this clearly: local providers work best for chat-style turns; tool-heavy workflows may degrade. The provider abstraction handles the translation, but per-model behavior is the user's to verify.

## Scope IN

- **OllamaProvider implementation:**
  - HTTP client to `http://localhost:11434` (configurable)
  - Streaming via Ollama's `/api/chat` SSE endpoint
  - Function calling translation: Ollama's native tools format (supported in newer versions)
  - Model selection: queries `/api/tags`, populates dropdown
- **LlamaCppProvider implementation:**
  - HTTP client to `http://localhost:8080` (configurable)
  - Streaming via llama.cpp's OpenAI-compatible endpoint
  - Function calling: requires model + serving config that supports it (user responsibility)
- **LMStudioProvider implementation:**
  - HTTP client to `http://localhost:1234/v1` (configurable, OpenAI-compatible)
  - Streaming + function calling via OpenAI-compatible API
  - Model selection: queries `/v1/models`
- **Settings → Account local section:**
  - Three adapters with endpoint + default model config
  - Connection test per adapter
  - "What is this?" link to docs explaining setup
- **Documentation:** clear caveats about local-model tool-calling quality
- **Subagent gating:** local providers expose `supportsSubagents: false` (matches OpenAI/Gemini behavior from F21)
- **Token / cost accounting:** local has no monetary cost; cost panel shows "0.00" with a note ("local execution")

## Scope OUT (deferred to v2.4+ or wontfix)

- **Auto-detection of running local servers** — manual config only; no scanning for Ollama / LM Studio processes
- **Embedded model serving** — app does not ship its own model runtime
- **Model download / installation UX** — user installs Ollama / LM Studio separately
- **Model fine-tuning workflow** — out of scope; user manages via the local runtime
- **Quality fallback to cloud** — no "if local fails, try cloud"; provider lock per session continues
- **Multi-GPU / remote local server (LAN-shared)** — single-localhost only initially
- **Mixed-provider conversations** ("delegate this turn to local for speed, that one to cloud for quality") — F35+ if ever

## Dependencies

- **F21 (Multi-LLM)** — must ship. F34 is implementations of F21's interface.

## Risks

- **Local-model tool-call reliability** — most local models still struggle with structured function calling at high quality. Users may have a bad first experience. Mitigation: clear docs, conservative messaging in the UI ("Local providers: experimental; chat works well; tool-heavy workflows may need a cloud provider"), model-selection guidance per provider.
- **Local server availability detection** — if Ollama / LM Studio isn't running, the provider's "Connect" call fails. Mitigation: pre-flight connection test + clear error message ("Ollama not detected at <url> — start it and refresh").
- **Latency variance** — local models on weaker hardware are slower than cloud. UX shouldn't make assumptions about turn duration. Mitigation: existing streaming + cancel infrastructure handles arbitrary turn lengths.
- **Tool translation edge cases** — local models may emit malformed function calls that the translation layer doesn't handle gracefully. Mitigation: defensive parsing, surface raw responses on parse failure for debugging.

## Open questions

1. **Should the app try to provide model recommendations per adapter?**
   - Recommendation: yes, conservative recommendations in the docs ("we've tested `qwen2.5-coder:32b` for Ollama, `gpt-4o-compatible` for LM Studio") — but no enforced model lists.
2. **What about Anthropic-compatible local servers (e.g., a local proxy that emulates Claude API)?**
   - Recommendation: use the OpenAI-compatible LM Studio adapter or document a custom endpoint pattern for ClaudeProvider with a custom base URL. Don't create a fourth adapter.
3. **Should local providers be hidden behind a "show advanced" toggle?**
   - Recommendation: no. Just listed under "Local" alongside "Cloud". If users feel overwhelmed, F32 onboarding skips local; local users opt in via Settings.
