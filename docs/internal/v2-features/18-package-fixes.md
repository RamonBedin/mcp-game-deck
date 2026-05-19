# Feature 18 — Package Tooling Fixes

## Status

`proposed` — design pending Ramon approval. Companion specs (`18-package-fixes-spec.md` + `18-package-fixes-tasks.md`) will follow when execution starts.

## Problem

Two tools in the `package-*` family are inconsistent with their siblings and break user expectations:

**(a) `package-add-registry` doesn't dedupe.** Every call appends a new entry to `Packages/manifest.json`, even if a registry with the same `name`+`url` already exists. Repeated calls pollute the manifest with duplicates. The tool can't be marked `IdempotentHint=true` (as it should be) because the second call has a different effect than the first (more entries).

**(b) `package-remove-registry` doesn't actually mutate the manifest.** It validates input and returns *instructions* telling the user to manually edit `Packages/manifest.json` — but it doesn't perform the removal. Currently marked `ReadOnlyHint=true` to reflect actual behavior. This creates asymmetry: the rest of the `package-*` family (`package-add`, `package-embed`, `package-resolve`, `package-remove`) all mutate directly. The LLM, generalizing from siblings, expects `package-remove-registry` to mutate and is confused when it doesn't.

Decision recorded during cycle 2 attempt: option A — implement actual mutation (rather than rename to `package-validate-registry-removal`, which would be a band-aid since the LLM will still need a mutating tool eventually).

## Proposal

Two surgical fixes, both in `Editor/Tools/Package/`.

**(a) Dedup on add.** Before appending the new registry entry, check if an entry with the same `name` (or `name`+`url` if `name` collisions are possible) already exists in `scopedRegistries`. If yes, return success without changing the manifest (or update the existing entry if `url`/`scopes` differ — design decision in spec phase). Re-mark `[McpTool]` with `IdempotentHint=true`.

**(b) Mutate on remove.**
1. Read `Packages/manifest.json`
2. Locate the registry entry by `name`
3. Remove it from the `scopedRegistries` array
4. Write back the file (atomic write — temp file + rename)
5. Update `[McpTool]` attribute: remove `ReadOnlyHint=true`, add `IdempotentHint=true` (removing twice is the same end-state as removing once — second call returns success without changing the file).

Both tools update the destructive sweep report to reflect their new annotations.

## Scope IN

- **`package-add-registry` dedup:**
  - Dedup by registry `name`+`url` (or just `name` — decide in spec; `name` alone may be sufficient since Unity package registry names are conventionally unique per source)
  - Return success with the existing entry if already present
  - `[McpTool]` attribute: add `IdempotentHint=true`
- **`package-remove-registry` mutation:**
  - Actual removal from `Packages/manifest.json` `scopedRegistries` array
  - Atomic write (temp file + rename) to avoid partial corruption
  - `[McpTool]` attribute: remove `ReadOnlyHint=true`, add `IdempotentHint=true`
- **Sweep report update:** the destructive sweep report `Editor/.../destructive-sweep-*.md` must reflect the new annotations for both tools (no longer "low-confidence" cases).
- **Validation:**
  - Add: call `package-add-registry` twice with same `name`+`url`; verify manifest has one entry, not two
  - Remove: call `package-add-registry` to add a test registry, call `package-remove-registry`, verify entry is gone; call remove again, verify no-error idempotent
  - Both: verify Unity Package Manager UI reflects the manifest changes after each call

## Scope OUT (deferred to v2.1+)

- **Pre-validate registry URL reachability** — don't HTTP-ping the registry on add; let user discover via Package Manager refresh if URL is wrong.
- **Conflict resolution UI** — if add finds an existing registry with the same name but different URL, return an error explaining the conflict; don't offer interactive resolution.
- **Backup of `manifest.json` before mutation** — atomic write is sufficient; if user needs history, that's what git is for.
- **Other `package-*` consistency issues** — narrow scope to add-registry and remove-registry. If other tools in the family have similar issues, surface them as separate KIs.
- **Scope name validation** — add-registry doesn't validate that `scopes` entries are valid Unity package scope strings; assume the user knows.

## Dependencies

None. F18 is fully independent. Smallest feature in the cycle, possibly 2 days of work including specs/tasks.

## Risks

- **`manifest.json` write race** — if Unity Package Manager is doing its own write at the moment the tool writes, file corruption is possible. Atomic write mitigates: write to temp, rename over original. Rename is atomic on Windows/Linux file systems.
- **Schema drift** — if Unity changes `scopedRegistries` schema in a future Unity version, the tool's read/write logic may break. Mitigation: read the file as untyped JSON (not strongly-typed model), preserve unknown fields verbatim, only touch `scopedRegistries` entries.
- **`name` vs `url` dedup ambiguity** — if two registries with same `name` but different `url` exist (technically valid?), dedup-by-name only would treat them as one. Decide in spec phase based on Unity's actual constraints; likely `name` alone is the de-facto unique key.

## Open questions

1. **Should dedup on add update the existing entry if `url` or `scopes` differ?**
   - Recommendation: yes, update. Treat the call as "ensure this registry config matches what I'm sending". User intent of a second add with different params is almost certainly "update". Document the behavior in tool description.
2. **Should remove also accept removal by `url` (not just by `name`)?**
   - Recommendation: not in v2.0. By-name is canonical; by-url is an edge case (and ambiguous if two entries share a url).
3. **Should both tools log a notice line in the C# console for traceability?**
   - Recommendation: log at Info level — useful for debugging. Not user-visible by default.

## Related cycle 2 attempt notes

The cycle 2 attempt identified both issues (as KI-001 and KI-002) and recorded the option-A decision for remove-registry (implement, don't rename). Implementation patches did not land before the working tree was discarded. This feature implements from scratch following the design above.

References for the implementation:
- `Editor/Tools/Package/Tool_PackageAddRegistry.cs`
- `Editor/Tools/Package/Tool_PackageRemoveRegistry.cs`
- Destructive sweep report (F19) low-confidence cases section
