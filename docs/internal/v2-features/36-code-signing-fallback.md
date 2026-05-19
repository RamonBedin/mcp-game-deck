# Feature 36 — Windows Code Signing Fallback (Azure Key Vault)

## Status

`proposed` — exploratory. Contingency feature; only executed if SignPath Foundation path fails.

## Problem

F16 (Auto-update infrastructure) shipped without Windows code signing. The plan: apply to SignPath Foundation's free OSS sponsorship program, which provides signing via their managed infrastructure. Approval takes weeks; v2.0 ships unsigned; users see SmartScreen warnings ("publisher unknown — run anyway?").

Risk: if SignPath denies the application, takes longer than expected, or the sponsorship lapses later, the project has no signing path. SmartScreen warnings hurt adoption — every new install requires the user to click through warnings, and corporate environments often block unsigned binaries via group policy.

F36 documents the fallback strategies and provides a working implementation path if needed.

## Proposal

Three documented fallback paths, in order of preference:

**(a) Azure Key Vault + signtool via GitHub Action (paid; recommended fallback).**
- Cost: ~$300/year for the certificate (Sectigo or DigiCert standard OV cert) + Azure Key Vault hosting (~$50/year).
- Setup: Purchase OV cert, upload to Azure Key Vault, configure GitHub Action with Azure service principal to sign release binaries.
- Pros: integrated into existing GitHub Actions workflow, no manual signing per release, cert is OV-validated (works for most cases).
- Cons: ~$350/year recurring; SmartScreen still shows initial warning for OV cert until "reputation" builds up over ~hundreds of downloads.

**(b) EV certificate with USB token / HSM (more expensive; no SmartScreen warning).**
- Cost: ~$500–700/year for EV cert + HSM device or cloud HSM.
- Setup: Purchase EV cert from issuer; sign locally via the HSM device, OR use cloud HSM for CI/CD signing.
- Pros: EV certs immediately bypass SmartScreen "publisher unknown" — perceived quality boost.
- Cons: more expensive; if local HSM, manual signing required (can't fully automate); CI/CD with cloud HSM still uses Azure Key Vault or similar managed service.

**(c) Standard cert via Sectigo / DigiCert with direct upload (intermediate option).**
- Cost: ~$300/year for OV cert; no Azure Key Vault if signing locally.
- Setup: Sign locally with a `.pfx` file on the maintainer's machine; CI/CD doesn't sign (manual upload of signed binary).
- Pros: cheapest of the working options; full control.
- Cons: manual signing per release; can't be automated cleanly in CI/CD.

**Recommended fallback: (a)** — Azure Key Vault + signtool. Balances cost, automation, and SmartScreen progression. Path (b) is justified if SmartScreen warnings prove demonstrably harmful to adoption.

## Scope IN (contingency execution — only if SignPath fails)

- **ADR-003:** document the decision matrix and chosen fallback path
- **Azure Key Vault setup guide:** step-by-step in `docs/internal/operations/code-signing-azure.md`
- **GitHub Action workflow extension:** modify `.github/workflows/release.yml` to add a signing step using `azure/azure-key-vault-action` (or equivalent) before publishing the release artifacts
- **`CODE_SIGNING.md` update:** reflect the chosen path (replace SignPath references with Azure Key Vault details where applicable)
- **Cert renewal calendar:** documented annual renewal reminder; cert expiration mid-cycle would silently break signed updates

## Scope OUT (deferred or wontfix)

- **Multi-cert fallback** (sign with both SignPath and Azure as redundancy) — single cert at a time
- **macOS code signing / notarization** — already on Apple Developer Program path; separate concern
- **Linux signing** (RPM / DEB signing keys) — not applicable to current distribution model
- **Custom CA / self-signed for internal corporate distribution** — out of scope
- **Real-time SmartScreen reputation telemetry** — accept Microsoft's opaque process
- **Automatic key rotation** — annual manual renewal continues

## Dependencies

- **F16 (Auto-update infrastructure)** — shipped. F36 is signing-only; updater workflow exists.
- **SignPath Foundation application outcome** — only execute F36 if SignPath denies or lapses. While SignPath is active, F36 stays unscoped.

## Risks

- **Cert compromise / private key exposure** — if Azure Key Vault credentials leak, attacker can sign malicious binaries as us. Mitigation: Azure Key Vault HSM-backed tier, audit logs, GitHub Action uses Azure service principal with minimal scope.
- **Cost escalation** — if user growth doesn't justify $350/year initially, sunken cost. Mitigation: cost is small relative to time invested in the project; accept as overhead.
- **Renewal lapse** — forgetting to renew the cert mid-cycle means subsequent signed updates fail. Mitigation: calendar reminder 60 days before expiration; documented runbook.
- **Cert chain trust failures on older Windows** — some pre-Windows-10 systems may not trust newer issuer chains. Acceptable; target Windows 10+ only.

## Open questions

1. **Which cert issuer?**
   - Recommendation: Sectigo (formerly Comodo) — common, mainstream chain, cheaper than DigiCert. Verify chain trust on Windows 10/11 during purchase trial.
2. **Should the EV path be planned as a v2.4 upgrade regardless of OV adoption?**
   - Recommendation: revisit after one year of OV signing. If SmartScreen reputation builds adequately, no need for EV. If still hostile, upgrade.
3. **Cloud HSM vs self-managed Azure Key Vault HSM tier?**
   - Recommendation: Azure Key Vault HSM-backed (Premium tier). $1/key/month, audit-friendly, integrates cleanly with GitHub Actions. Self-managed HSM is over-engineering.

## Related notes

This is purely a contingency feature. It only enters active development if SignPath denies the sponsorship, OR if SignPath approval doesn't arrive by some pragmatic cutoff (e.g., 4 months after application), OR if the sponsorship lapses for any reason at a later date.

The expected primary path remains SignPath Foundation; F36 design exists so that the contingency isn't a scramble when needed.

Documentation work alone is mostly the deliverable. Implementation (GitHub Action signing step) is ~1 day of focused work given Azure Key Vault is well-documented.
