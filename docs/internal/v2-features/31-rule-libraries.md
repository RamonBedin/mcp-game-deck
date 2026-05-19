# Feature 31 — Rule Libraries Cross-Project

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

After F26 (rules functional + auto-suggestion), each project has a working rule set. But conventions repeat across projects: "use TextMeshPro, not legacy Text" is just as true in project A as in project B. C# style rules, namespacing rules, Unity-version-specific gotchas — none of these are project-specific. Yet today each new project starts with a blank rule slate.

Three friction points:

**(a) No cross-project library.** A user's hard-won rule set from project A doesn't transfer to project B without manual file copy.

**(b) No versioned library subscription.** Studios with internal conventions evolving over time need a way to push rule updates to all their projects from a central source. Today: manual sync, easy to drift.

**(c) No community discovery.** Generic Unity conventions (URP best practices, addressables patterns, common pitfalls) are valuable across the whole user base. No way to find or share these.

## Proposal

Three layered capabilities, similar shape to F30 (plans templates / sharing / versioning).

**(a) Personal library.** A new app-level (not per-project) rules store at `<user-config-dir>/MCPGameDeck/rule-libraries/personal/`. Rules in the personal library can be imported into any project. The user manages the personal library from a Settings page distinct from the per-project Rules page.

**(b) Library subscriptions.** Studios / teams can publish a rule library to a git repo, an HTTPS endpoint, or a `.rulelib.json` bundle. Projects can subscribe to one or more libraries. On app startup (or manual refresh), subscribed libraries are pulled and made available read-only in the project's rule resolution chain. Resolution order: per-user > per-project > subscribed library > built-in defaults.

**(c) Community library catalog.** A read-only catalog of community-contributed libraries hosted by the maintainer (e.g., GitHub Pages site listing curated libraries). Browse / preview / subscribe to community libraries from within the app. Trust model: libraries are signed by their author + reviewed before listing; users verify they trust the source before subscribing.

**Out of scope for v2.3 initial:**
- Library publishing UI within the app (use git / static hosting; in-app publisher is v2.4+)
- Rule conflict resolution beyond "first-in-chain wins" (no merge UI)
- Library forking / customization (subscribe + customize requires copy locally)

## Scope IN

- **Personal library:**
  - Storage path: OS-appropriate (`%APPDATA%/MCPGameDeck/rule-libraries/personal/` on Windows, `~/Library/Application Support/...` on macOS, `~/.config/...` on Linux)
  - Settings page to manage personal library (CRUD)
  - "Import to project" from personal library: copies rule into project's `ProjectSettings/GameDeck/rules/`
- **Subscriptions:**
  - Subscription source: git URL, HTTPS URL, or local file path
  - `.rulelib.json` bundle format: `{version, library_name, author, rules: [...]}`
  - On app boot + on manual refresh: pull subscribed libraries (clone or fetch)
  - Cache at `<user-config-dir>/MCPGameDeck/rule-libraries/subscribed/<library-name>/`
  - Read-only — user can't modify subscribed library rules from the app; they edit the source
  - Resolution chain integration: subscribed libraries layer between per-project and built-in
- **Community catalog (read-only):**
  - Catalog URL configurable, default points to maintainer-hosted site
  - "Browse community libraries" Settings page
  - Per-library detail view: name, author, description, rule count, last updated, trust badge if maintainer-verified
  - "Subscribe" button adds to the user's subscription list
- **Conflict surfacing:**
  - Effective rule view shows source per rule ("from `unity-urp-conventions` library")
  - If two libraries define conflicting rules, first-in-chain wins; UI shows the conflict

## Scope OUT (deferred to v2.4+ or wontfix)

- **Library publishing from within the app** — git push / HTTPS upload via UI
- **Rule conflict merge UI** — manual resolution by reordering subscription list
- **Anonymous community contribution** — all listed libraries have known authors
- **Paid / commercial library marketplace** — non-commercial focus initially
- **Library analytics for publishers** — subscriber counts, popular libraries
- **Auto-update libraries** — pull on boot only, no background refresh
- **Library dependencies** ("library X depends on library Y") — flat model

## Dependencies

- **F26 (Rules functional)** — must ship. F31 builds on the rule resolution chain F26 establishes.

## Risks

- **Trust model for subscribed libraries** — a malicious library could embed harmful normative instructions. Mitigation: subscribed libraries are read-only and clearly attributed; permission rules (F23) bound blast radius; maintainer-curated catalog reduces drive-by exposure.
- **Version compatibility** — a library author publishes a rule using a syntax v2.4 supports, but a v2.3 user subscribes. Mitigation: rule format version field, skip unsupported entries with a warning.
- **Subscription cycles** — library A references library B which references A. Mitigation: flat subscription model (no library deps) in v2.3 makes this impossible.
- **Network reliability** — first launch on a flaky connection can't fetch subscribed libraries. Mitigation: cached versions persist; failed fetch surfaces a warning but doesn't block use.

## Open questions

1. **Should subscribed libraries auto-update or require manual refresh?**
   - Recommendation: manual + opt-in auto-update per library. Users want stability for foundational libraries.
2. **Where does the community catalog live?**
   - Recommendation: maintainer-hosted (`mcp-game-deck.dev/libraries/index.json` or similar). Static JSON file, free to host, easy to curate via PR.
3. **Can a user mix personal + subscribed + project rules freely?**
   - Recommendation: yes, all layer in the resolution chain. Settings shows the full chain order; rules can be reordered by drag.
