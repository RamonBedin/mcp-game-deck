# Feature 35 — Enterprise Endpoints (Bedrock, Vertex, Azure OpenAI)

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

Enterprise / studio adoption is gated by provider routing assumptions. Three common cases:

- **AWS-shop studios** — already using Anthropic Claude via AWS Bedrock with billing, audit logs, IAM controls integrated into their AWS infrastructure. Can't switch to direct Anthropic API consumption without re-doing procurement.
- **GCP-shop studios** — same scenario via Google Vertex AI.
- **Microsoft-shop / enterprise OpenAI users** — Azure OpenAI Service with regional deployments, content filtering policies, and Azure AD authentication.

Today's F21 (Multi-LLM) ships with consumer endpoints only. Enterprise teams literally cannot use the app even if they're willing to pay — the auth schemes and base URLs don't match their procurement-approved infrastructure.

## Proposal

Extend each provider in F21 with per-instance endpoint configuration:

**`ClaudeProvider` variants:**
- Direct Anthropic API (default)
- AWS Bedrock (`anthropic.claude-3-5-sonnet-v2:0` etc. on Bedrock; AWS Sig V4 auth)
- GCP Vertex (`anthropic.claude-3-5-sonnet@20241022` etc. on Vertex; GCP service account auth)

**`OpenAIProvider` variants:**
- Direct OpenAI API (default)
- Azure OpenAI Service (custom base URL per Azure deployment, Azure auth with API key or AAD token)

**`GeminiProvider` variants:**
- Google AI Studio (default — consumer)
- GCP Vertex AI (enterprise; service account auth)

Auth credentials per variant stored in Tauri Stronghold (extending F21's key storage). Settings UI gets a "Connect endpoint" wizard per provider that lets the user pick the variant and provide credentials in a guided flow.

## Scope IN

- **ClaudeProvider — Bedrock variant:**
  - AWS SDK integration (`@aws-sdk/client-bedrock-runtime`)
  - AWS Sig V4 signing on requests
  - Region + access key + secret key + optional session token storage
  - Bedrock-specific model IDs in selector
- **ClaudeProvider — Vertex variant:**
  - Google Cloud Auth (`google-auth-library`)
  - Service account JSON storage (encrypted in Stronghold)
  - Region / project ID configuration
  - Vertex-specific model IDs
- **OpenAIProvider — Azure variant:**
  - Azure OpenAI base URL pattern: `https://<resource>.openai.azure.com/openai/deployments/<deployment>/...`
  - API key or AAD token auth
  - Deployment name + API version configuration
- **GeminiProvider — Vertex variant:**
  - Reuses Vertex auth from Claude variant
  - Vertex-specific Gemini model IDs
- **Settings UI extensions:**
  - Endpoint variant selector per provider
  - Guided credential entry per variant (with clear "where to find this" docs)
  - "Test connection" per variant
  - Save under named profile (e.g., user can have "personal Anthropic" + "work Bedrock" profiles)
- **Profile switching:** session-level provider selection (from F21) gains profile granularity ("Claude (personal)" vs "Claude (work Bedrock)")
- **Documentation:** per-variant setup guides (where to get credentials, IAM permissions needed, etc.)

## Scope OUT (deferred to v2.4+ or wontfix)

- **AWS / GCP / Azure SSO integration** — credential entry is manual; SSO auth flows are a separate complexity
- **Cross-region failover** — single endpoint per profile; failover is user-managed
- **Cost dashboards integrated with cloud billing APIs** — analytics dashboard (F33) shows app-side estimated cost only; cloud billing reconciliation is the user's responsibility
- **Per-conversation provider profile selection** — session-level lock from F21 continues
- **VPC / private endpoint support** — public cloud endpoints only initially
- **HIPAA / SOC compliance attestations** — out of scope; users responsible for their own compliance posture
- **Spend governance / quotas** — no app-side spending limits enforced

## Dependencies

- **F21 (Multi-LLM)** — must ship. F35 extends F21 providers with variants.

## Risks

- **SDK weight** — adding AWS SDK + Google auth + Azure libs to the app bundle bloats binary size noticeably. Mitigation: tree-shake aggressively; if total size exceeds ~150 MB, split SDKs into lazy-loaded modules.
- **Credential leakage** — enterprise credentials are higher-stakes than consumer API keys. Mitigation: Stronghold storage, never log credentials, mask in any error messages.
- **Auth flow drift** — cloud providers update auth SDKs / deprecation cycles. Mitigation: regular dependency updates, version-pin only major versions, deprecation warnings in app.
- **Per-variant testing overhead** — testing 5+ variants against real cloud endpoints during development is expensive (real API calls). Mitigation: integration tests via mocked endpoints; real-endpoint smoke tests pre-release only.

## Open questions

1. **Should the app surface a "managed by your studio admin" mode where credentials come from a config file?**
   - Recommendation: not in v2.3 initial. Manual entry only. If studios push back, follow-up with admin-config in v2.4.
2. **Profile names — free-form or constrained?**
   - Recommendation: free-form. "personal", "work-bedrock", "client-acme" — user's choice.
3. **What about Anthropic's own enterprise tier (direct, with custom limits)?**
   - Recommendation: uses default ClaudeProvider with the user's enterprise API key. No new variant; the consumer path handles it.
