# Decision — No AssetBundle tooling in Asset domain

**Date:** 2026-05-19
**Status:** Accepted
**Source:** Asset audit finding G3 (`.claude/reports/audits/audit-Asset-20260425.md`), Asset review escalation E4 (`.claude/reports/reviews/review-Asset-20260519.md`).

## Decision

The Asset domain MCP tooling will not wrap Unity's AssetBundle APIs
(`AssetImporter.assetBundleName`, `assetBundleVariant`, related `BuildPipeline`
calls). G3 from the Asset audit is marked `won't-fix`.

## Rationale

AssetBundles are a legacy distribution mechanism. Addressables has been
Unity's recommended workflow for several major versions and is the
forward-looking story for Unity 6000+ projects. Adding tooling for a
deprecated workflow adds long-term maintenance burden for negligible value.

The audit specifically noted (G3, Confidence: medium) that the API was not
wrapped anywhere in the project and that the relevance of AssetBundles in
2026 is itself in question.

## Scope of this decision

- **In scope:** AssetBundle import-settings (`assetBundleName`,
  `assetBundleVariant` on `AssetImporter`), `BuildPipeline.BuildAssetBundles`
  related tooling.
- **Out of scope (separate decision if requested):** Addressables tooling
  (`AddressableAssetSettings`, group management, build profiles). If a future
  cycle wants Addressables coverage, it lives as its own domain. Addressables'
  API surface is significantly larger and warrants its own audit/review/plan
  cycle.

## Future re-evaluation triggers

Re-open this decision only if:

1. Unity removes the Addressables package or deprecates it.
2. A concrete user workflow surfaces that explicitly requires AssetBundles
   over Addressables (e.g. a third-party tool integration that only consumes
   AssetBundles).

Otherwise this decision stands.

## References

- Asset audit G3: `.claude/reports/audits/audit-Asset-20260425.md` (Section 5)
- Asset review E4: `.claude/reports/reviews/review-Asset-20260519.md` (Section 9)
- Unity Addressables docs (forward-looking story)
