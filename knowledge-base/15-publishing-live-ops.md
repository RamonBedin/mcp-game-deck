# 15 — Publishing & Live-Ops for Unity Games (2025-2026)

Practical guide for publishing, operating, and growing Unity games on mobile, PC, and web platforms. Covers everything from the build pipeline to post-launch, with a focus on indie studios and small teams.

---

## Table of Contents

1. [Build Pipeline](#1-build-pipeline)
2. [CI/CD](#2-cicd)
3. [Publishing by Platform](#3-publishing-by-platform)
4. [Soft Launch](#4-soft-launch)
5. [Analytics](#5-analytics)
6. [Indie Monetization](#6-indie-monetization)
7. [Basic Live-Ops](#7-basic-live-ops)
8. [Post-Launch](#8-post-launch)
9. [Platform Checklists](#9-platform-checklists)
10. [Benchmark Metrics by Genre](#10-benchmark-metrics-by-genre)
11. [Recommended Tools](#11-recommended-tools)
12. [Sources and References](#12-sources-and-references)

---

## 1. Build Pipeline

### 1.1 Optimized Build Settings by Platform

#### Android

- **Required format**: Android App Bundle (AAB) — Google Play no longer accepts APKs for new apps.
- **Scripting Backend**: IL2CPP (required for ARM64, better performance than Mono).
- **Target API Level**: Android 15 (API 35) required from August 2025 for new apps and updates.
- **Texture Compression**: ASTC is the recommended format for modern devices (Adreno 4xx+, Mali T624+). ETC2 as fallback for older hardware (OpenGL ES 3.0+).
- **Play Asset Delivery (PAD)**: Use to split assets and reduce the initial bundle size. Works with Addressables.
- **Tip**: Exporting as a Gradle project and importing into Android Studio gives more control over manifests and build customization.

#### iOS

- **Scripting Backend**: IL2CPP (required).
- **Minimum SDK**: From April 2026, apps must use the iOS/iPadOS 26 SDK.
- **Texture Compression**: ASTC (primary) and PVRTC (legacy).
- **Bitcode**: Check current Apple requirements (has changed between versions).
- **Provisioning**: Distribution certificates + provisioning profiles via the Apple Developer Program ($99/year).

#### PC / Steam

- **Scripting Backend**: IL2CPP recommended for releases (Mono is acceptable for quick dev builds).
- **Steam Deck**: Support 1280x800 or 1280x720, full controller support, readable text on small screens.
- **Build targets**: Windows x64 (primary), Linux x64 (native Steam Deck), macOS (optional).

#### WebGL (itch.io / demos)

- **Scripting Backend**: IL2CPP (the only option supported for WebGL).
- **Compression**: Brotli for hosting that supports it, Gzip as fallback.
- **Code Generation**: "Optimize for code size and build time" — significantly reduces WebGL build size.

### 1.2 Build Size Optimization

| Technique | Impact | When to Use |
|-----------|--------|-------------|
| **IL2CPP Managed Stripping Level "High"** | Significant reduction of the executable | Always in release builds |
| **Addressables** | On-demand loading, delta updates | Projects with many assets |
| **Texture Compression (ASTC)** | Up to 75% smaller than raw textures | All modern platforms |
| **Sprite Atlas** | Reduces draw calls and size | 2D games |
| **Audio: Vorbis (mobile) / MP3 (web)** | Efficient audio compression | Always |
| **Organized Asset Bundles** | Avoids loading everything into memory | Content-heavy games |
| **Code stripping + link.xml** | Removes unused code | Always, with care around reflection |

**Case study**: An indie studio reduced a mobile build from 500MB to 50MB using a combination of Addressables, texture compression, and asset bundle organization.

### 1.3 IL2CPP Stripping Levels

IL2CPP always performs bytecode stripping regardless of configuration. The Managed Stripping Level options control how aggressive the stripping is:

- **Minimal**: Removes only clearly unused code. Safe, but larger build.
- **Low**: Moderate stripping. Good balance for development.
- **Medium**: Removes more code. Can break reflection — use `link.xml` to preserve types.
- **High**: Maximum stripping. **Recommended for release** (especially WebGL/mobile). Requires a well-configured `link.xml`.

**Code Generation Options**:
- "Optimize for runtime speed" (default): More machine code, slower builds, better runtime.
- "Optimize for code size and build time": Less code, faster builds, smaller binaries — **ideal for WebGL and mobile**.

### 1.4 Automated Builds

#### Unity Build Automation (Cloud Build)

- Managed service by Unity, cloud CI/CD.
- **Pricing (March 2026)**: Free tier with 2 concurrent build machines, 25 GB storage, 100 GB egress. 200 min Windows, 100 min macOS, 100 min Linux/month.
- **Pros**: Minimal setup, native multi-platform support.
- **Cons**: Cost scales quickly above the free tier.

#### GitHub Actions + GameCI

- **GameCI** is open-source and free.
- Supports: Windows, macOS, Linux, iOS, Android, WebGL.
- Key actions: `game-ci/unity-test-runner@v4` and `game-ci/unity-builder@v4`.
- Publishes directly to Google Play, macOS App Store, and S3.

#### Jenkins

- Self-hosted, free, full control.
- Requires the Unity3D Plugin on build nodes.
- Essential flags: `-quit -batchmode -nographics`.
- **Tip**: Use a single executor per build server to avoid conflicting simultaneous builds.

---

## 2. CI/CD

### 2.1 GameCI: Unity + GitHub Actions

**Typical workflow:**

```yaml
name: Unity CI
on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true
      - uses: actions/cache@v3
        with:
          path: Library
          key: Library-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}
      - uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          testMode: all

  build:
    needs: test
    runs-on: ubuntu-latest
    strategy:
      matrix:
        targetPlatform: [Android, iOS, WebGL, StandaloneWindows64]
    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true
      - uses: actions/cache@v3
        with:
          path: Library
          key: Library-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}
      - uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
        with:
          targetPlatform: ${{ matrix.targetPlatform }}
      - uses: actions/upload-artifact@v3
        with:
          name: Build-${{ matrix.targetPlatform }}
          path: build
```

**Licensing**: Configure `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` as GitHub Secrets.

### 2.2 Automated Testing in the Pipeline

**Unity Test Framework (UTF) v1.1.33** (stable, based on NUnit 3.5):

| Test Type | What It Tests | Speed | When to Use |
|-----------|---------------|-------|-------------|
| **EditMode** | Pure logic, utils, data validation | Fast | Always — it's the foundation |
| **PlayMode** | MonoBehaviour lifecycle, gameplay systems | Slow | Gameplay systems, integration |

**What to test first in indie games:**
- Save/load systems
- Damage/economy calculations
- Progression and unlocks
- IAP purchases (mock)
- Gameplay state machines

### 2.3 Full Pipeline: Build → Test → Deploy

```
Commit → Lint/Format Check → EditMode Tests → PlayMode Tests → Build (multi-platform) → Upload Artifacts → Deploy (staging/production)
```

**CI/CD tool comparison:**

| Tool | Cost | Setup | Best For |
|------|------|-------|----------|
| **GameCI + GitHub Actions** | Free | Medium | Teams using GitHub |
| **Unity Build Automation** | Metered (generous free tier) | Low | Cloud convenience |
| **Jenkins** | Free (self-hosted) | High | Full control, enterprise |
| **GitLab CI/CD** | Free (GitLab) | Medium | Teams using GitLab |

---

## 3. Publishing by Platform

### 3.1 Google Play Store

**Current requirements (2025-2026):**
- Android App Bundle (AAB) required.
- Target API level 35 (Android 15) since August 2025.
- Play Asset Delivery for apps >150MB.
- Privacy policy required.
- Data safety form filled out.
- Age rating via IARC.

**Review Process:**
- Average time: 12-24 hours.
- Typical range: 1-3 days for new apps.
- Sensitive categories (finance, kids): up to 7 days.
- **Recommendation**: Plan a 3-5 day buffer in your launch schedule.

**ASO (App Store Optimization) 2026:**
- The algorithm now prioritizes **retention over install volume** — if users leave quickly, ranking drops.
- Keywords: one exact match every ~250 characters in the description; no keyword stuffing.
- Android Vitals (crash rate, battery, responsiveness) directly impact ranking.
- Screenshots and visual creatives strongly influence conversion.
- Monitor and optimize continuously with tools like AppTweak or ASOLYTICS.

### 3.2 Apple App Store

**Current requirements (2025-2026):**
- Minimum SDK: iOS/iPadOS 26 SDK from April 2026.
- Apple Developer Program: $99/year.
- New age rating system: 4+, 9+, 13+, 16+, 18+ (updated January 2026).
- Privacy Nutrition Labels required.
- Strict App Review Guidelines for game content.

**TestFlight:**
- Up to 100 internal testers (App Store Connect users) — no review required.
- Up to 10,000 external testers.
- The first build of each version requires an Apple review; subsequent builds do not.
- Builds available for 90 days.
- Testers can submit feedback via the TestFlight app.

**Provisioning:**
- Development certificates for test builds.
- Distribution certificates for App Store and Ad Hoc.
- Provisioning profiles link certificates to App IDs and devices.
- **Tip**: Use Fastlane (match) to automate certificate management in CI/CD.

### 3.3 Steam

**Steamworks SDK + Unity:**
- **Steamworks.NET**: Free, open-source C# wrapper (MIT), 100% API coverage.
  - Copy `com.rlabrecque.steamworks.net/` to Assets/.
  - Update `steam_appid.txt` with your AppId.
- **Toolkit for Steamworks**: Higher-level wrapper, available on the Asset Store.
- **Unity Authentication**: Steam sign-in via Unity's Authentication system.
- **Registration fee**: $100 per app (refundable after $1,000 in sales).

**Store Page Optimization:**
- **Capsule Art**: Readable at 120x45 pixels; bold colors with high contrast against Steam's dark UI.
- **Trailer**: Gameplay in the first 5 seconds; best hook in 2-3 seconds. One strong trailer > several mediocre ones.
- **Description**: Name your genre in the first 10 words of the short description.
- **Tags**: Use all 20 slots; specific sub-genres ("Precision Platformer") > generic ones ("Action").
- **Screenshots**: Show real gameplay variety — environments, mechanics, moods.
- **Coming Soon page**: Publish as early as possible to accumulate wishlists.
- **Localization**: 60%+ of Steam users use the platform in a non-English language.
- **Mobile traffic**: 35-45% of visits to indie titles come from the mobile storefront.

**Steam Deck Compatibility:**
- Categories: Verified, Playable, Unsupported, Unknown.
- Input: Full controller support required; glyphs must match Deck or Xbox buttons.
- Display: 1280x800 with readable text.
- Performance: 30-60 fps on native hardware.

### 3.4 itch.io (Prototypes and Demos)

- Upload the complete HTML5 export folder for WebGL builds.
- No download required = higher engagement for demos.
- Excellent for game jams and community building.
- No review process — instant publication.
- Use as a "proving ground" before investing in Steam/mobile.
- **Tip**: Set up a devlog on itch.io to build an audience before launch.

---

## 4. Soft Launch

### 4.1 What It Is and Why to Do It

A soft launch is releasing the game in limited markets (specific countries) before the worldwide launch. Objectives:

- Validate retention and monetization metrics with real users.
- Identify bugs and crashes in production.
- Test server load and infrastructure.
- Optimize onboarding and FTUE (First Time User Experience).
- Refine game economy and balance.
- Discover the game's "creative DNA" for marketing.

**Evolution in 2025-2026**: The soft launch has evolved from "cheap volume validation" to sophisticated fine-tuning with AI-driven creative discovery and optimized onboarding.

### 4.2 Key Metrics

| Metric | Median | Top 10% | Minimum Goal |
|--------|--------|---------|--------------|
| **D1 Retention** | 22% | 40%+ | 35% |
| **D7 Retention** | 4% | 12% | 15% |
| **D30 Retention** | ~2-3% | 10%+ | 5% |
| **Session Length** | Varies by genre | — | Monitor trend |
| **Crash Rate** | < 2% | < 0.5% | < 1% |

**Updated benchmark 2026**: The classic 40/20/10 reference (D1/D7/D30) has been adjusted to **35/15/5** as a realistic baseline. Excellence remains at 40+/20+/10+.

**Financial impact**: Improving D30 from 3% to 10% reduces cost-per-retained-user from $100 to $30.

### 4.3 Geo-Targeting for Soft Launch

| Country/Region | Why | Install Cost | When to Use |
|----------------|-----|-------------|-------------|
| **Canada** | Similar to US market, native English | Medium-high | Final validation before US |
| **Australia / New Zealand** | English-speaking, similar to US/UK | Medium | Good alternative to Canada |
| **Ireland / Netherlands** | Fluent English, lower costs | Medium-low | Initial retention tests |
| **Philippines / India** | English, very low costs | Low | High volume for stress testing |
| **Poland** | High midcore engagement, Android dominant | Low-medium | Midcore/male-oriented games |
| **Singapore** | ~1/3 the cost of Tier 1 | Low | Cost-effective retention tests |

**Recommended strategy:**
1. Home country for initial bug fixing (lowest cost, maximum convenience).
2. Singapore or Philippines for retention tests (low cost, volume).
3. Canada or Australia for final validation before global launch.

**Typical duration**: 4-12 weeks, depending on metrics and required iterations.

---

## 5. Analytics

### 5.1 Platform Comparison

| Aspect | Unity Analytics | GameAnalytics | Firebase Analytics |
|--------|----------------|---------------|--------------------|
| **Price** | Free up to 50k MAU; then $1k-$4k/month | Free up to 10k MAU; Pro $299/month | Completely free |
| **Setup** | Native in Unity | Simple SDK | Requires Firebase SDK |
| **Focus** | Unity ecosystem | Game metrics | Google ecosystem |
| **Funnel Analysis** | Yes | Yes (pre-configured) | Yes |
| **Cohort Analysis** | Yes | Yes | Yes (via BigQuery) |
| **Custom Events** | Up to 500 recommended | Unlimited | Up to 500 types |
| **Export** | Limited | Raw data (Pro) | BigQuery (1 click) |
| **Cross-platform** | Unity only | iOS, Android, PC | All |
| **Best for** | Unity projects < 50k MAU | Indies, mobile-first | Scale, Google ecosystem |

**Recommendation by phase:**
- **Prototype/Soft Launch**: GameAnalytics (free, ready-made game metrics) + Firebase Crashlytics.
- **Post-Launch (< 50k MAU)**: GameAnalytics + Unity Analytics.
- **Growth (50k-500k MAU)**: Firebase Analytics + GameAnalytics Pro.
- **Scale (> 500k MAU)**: Firebase with BigQuery + specialized tools (Keewano, ThinkingData).

**Emerging**: Keewano (AI-powered, 600x faster than traditional systems) and ThinkingData (used by 1,500+ game companies including FunPlus, SEGA, IGG).

### 5.2 Essential Custom Events

#### Universal Events (all games)

```
game_start              — Session start
game_end                — Session end
tutorial_start          — Tutorial start
tutorial_complete       — FTUE completion
tutorial_skip           — Tutorial skip (attention flag)
level_start             — Level/stage start
level_complete          — Completion with time and score
level_fail              — Failure with reason
```

#### Monetization Events

```
store_open              — Intent to spend
item_viewed             — Interest in a product
purchase_initiated      — Purchase start
purchase_completed      — Purchase completed (with value, currency, item)
purchase_failed         — Purchase failure (with reason)
ad_impression           — Ad shown
ad_click                — Ad clicked
ad_complete             — Rewarded video watched to completion
ad_skip                 — Ad skipped (interstitial)
```

#### Engagement Events

```
daily_login             — Daily login (with streak count)
feature_unlock          — Feature unlocked
social_share            — Social share
achievement_unlocked    — Achievement unlocked
difficulty_changed      — Difficulty change
progression_stalled     — No progress for X days
```

**Framework for planning events**: Use the GQM model (Goal-Question-Metric):
1. Define the business objective.
2. Frame it as a question (e.g., "Are players reaching monetization?").
3. Select the metric that answers the question.
4. Structure events in a hierarchy (categories/subcategories).

### 5.3 Funnel Analysis

**Essential funnels for games:**

1. **Onboarding Funnel**: Install → Open → Tutorial Start → Tutorial Complete → First Session End → D1 Return.
2. **Progression Funnel**: Level 1 → Level 5 → Level 10 → Level 20 (identifies difficulty spikes).
3. **Purchase Funnel**: Store Open → Item View → Purchase Initiated → Purchase Complete.
4. **Retention Funnel**: D1 → D3 → D7 → D14 → D30.

**Implementation**: 5-7 steps per funnel is ideal. Establish a drop-off baseline, segment by cohort/region/platform, monitor regressions after updates.

### 5.4 Cohort Analysis

Group players by shared characteristics for LTV and retention tracking:

- **By install date**: "Users who installed after the update have 5% better D7."
- **By acquisition channel**: Organic vs paid vs social.
- **By region**: Regional performance.
- **By app version**: Impact of updates.

**Typical output**: Retention table by cohort (D1, D3, D7, D30) with revenue metrics.

### 5.5 Dashboard Setup

**3-level architecture (pyramid):**

1. **Executive Summary** (top): DAU, Revenue, key KPIs — maximum 10-15 metrics.
2. **Trends & Analysis** (middle): Retention charts, funnel drop-offs, monetization trends.
3. **Detailed Breakdowns** (base): Regional performance, platform splits, segment deep-dives.

**Best practices:**
- Limit to 10-15 core metrics (avoid overload).
- Configure automatic alerts for threshold breaks (retention dropped > 5%, crash spike).
- Regional filters for geo-specific monitoring.
- Views by role (designers, PMs, executives see different dashboards).
- Auto-refresh for real-time monitoring.

---

## 6. Indie Monetization

### 6.1 Premium vs F2P vs Hybrid

| Model | When to Use | Revenue Share (2025) | Advantages | Challenges |
|-------|-------------|---------------------|------------|------------|
| **Premium** | PC indie, story-driven, niche | 60.12% of indie revenue | Player trust, simplicity, lower server cost | High discoverability barrier |
| **F2P** | Mobile, live-service, casual | Largest audience (mobile) | Zero friction, virality, scalable revenue | Requires constant updates, ethical monetization design |
| **Hybrid** | Mobile with IAP + ads, free trial + premium | Fastest growth (21.6% CAGR) | Diversifies revenue, respects different player profiles | More complex to implement and balance |

**2025 market data:**
- Indie market projected: $5.54B in 2026 → $10.83B by 2031 (14.32% CAGR).
- Premium dominates 60.12% of indie revenue in 2025.
- Subscription is the fastest-growing segment (21.6% CAGR).

### 6.2 Ethical Ads

#### Rewarded Video

- **Performance**: 3.5x higher engagement vs non-rewarded; 95%+ completion rate.
- **CPM**: $10-$20 (higher than interstitials).
- **Golden rule**: Always opt-in, never forced.
- **Key data (2025)**: Suppressing ads for 10 minutes after a watched rewarded video **increased overall revenue** by respecting the experience.

#### Interstitial

- **Timing**: Only at natural breaks (end of level, game over, menu transition).
- **Never**: Interrupt expected gameplay, show in the first 5 minutes, without a clear close button.
- **Maximum frequency**: 5-6 ads/hour for short-session games.
- **Warning**: Aggressive interstitials cause a 15-25% increase in churn in the first session.

#### Ethical Ads Framework

- **Right ad + Right user + Right time**: Segment by play style.
- Short sessions → rewarded videos (extend playtime).
- Long sessions → tolerate banners, interstitials only at natural breaks.
- Monitor churn against ad frequency constantly.

### 6.3 IAP (Unity IAP)

**Current state (2025-2026):**
- Unity IAP v5.0.0 (August 2025) for compliance with Google Billing Library ≥ 7.0.0.
- Ships with Billing Library v8.0.0.

**Product types:**
- **Consumable**: Multiple purchases (gems, currency, boosters).
- **Non-consumable**: One-time purchase (ad removal, premium features).
- **Subscription**: Recurring (battle pass, season pass, premium tier).

**Basic setup:**
1. Install via Package Manager: `Window > Package Manager > In-App Purchasing`.
2. Configure the catalog in Unity Services.
3. Implement the `UnityPurchasing` interface.
4. Handle `OnPurchaseComplete()` callbacks.
5. Validate receipts server-side via the Economy package (**essential against fraud**).

**Support**: Apple App Store (StoreKit 2), Google Play (Billing Library v7/v8), unified API.

### 6.4 Pricing Strategies

#### PC (Steam)

- **Indie sweet spot**: $15-$20 with quality exceeding expectations generates more organic momentum than $30 with expected quality.
- **Above $30**: Requires brand/fanbase; conversion rates drop significantly.
- **Premium DLC**: 77% of ongoing spend in a base premium + rich DLC model.
- **$30-$50 range**: Fastest growth (156% for new releases in 2025).
- **Psychological pricing**: $14.99 and $19.99 are the most effective price points.
- **Regional pricing**: Essential — price by PPP (Purchasing Power Parity), not flat USD.

#### Mobile

- **Premium mobile**: $9.99 (psychological pricing) is effective, but the premium mobile market is small.
- **F2P**: Heavy reliance on ads or IAP.
- **Hybrid**: Growing fastest, especially casual sims.

---

## 7. Basic Live-Ops

### 7.1 Remote Config

#### Firebase Remote Config vs Unity Remote Config

| Aspect | Firebase Remote Config | Unity Remote Config |
|--------|----------------------|---------------------|
| **Price** | Free | Free |
| **Features** | Conditional values (segment, region, version), BigQuery export, real-time rollback | Native Unity, simpler, lightweight |
| **Scale** | Hundreds of key-value pairs | Adequate for Unity projects |
| **Cross-platform** | Yes (strong) | Unity only |
| **Overhead** | Can have high GC allocations on low-end devices if misconfigured | Lighter on low-end devices |
| **Best for** | Google ecosystem, cross-platform, scale | Pure Unity projects, simplicity |

**Practical use cases:**
- Adjust difficulty without an update.
- Change IAP prices by region.
- Enable/disable features by version.
- Seasonal content (holiday themes, events).
- Emergency kill switches.

### 7.2 Feature Flags

Toggles that enable/disable features without a code deploy. Applications in games:

- Test new mechanics with a subset of players.
- Seasonal content without interruption.
- Region-specific content (special items by geography).
- Adaptive difficulty adjustments.
- A/B test weapons/mechanics instantly.
- Emergency rollback without a patch.

**Tools:**
- **Firebase Remote Config**: Works as a basic feature flag system.
- **ConfigCat**: Gaming-focused, easy to integrate.
- **LaunchDarkly**: Enterprise, more robust.
- **Flagsmith**: Open-source, self-hosted option.

**Best practices:**
- Clear naming convention (`feature_X_enabled`).
- Document the rollout schedule.
- Monitor kill-switch for critical features.
- Archive old flags regularly.

### 7.3 A/B Testing

**Popular tools for games (2025-2026):**

| Tool | Platform | Differentiator | Price |
|------|----------|----------------|-------|
| **Splitforce** | iOS, Android, Unity | Tests anything (UI, physics) | Freemium |
| **Taplytics** | iOS, Android | Live changes without App Store update | Free + paid |
| **Amplitude** | Multi-platform | Product analytics + A/B testing | Robust freemium |
| **Firebase Remote Config** | Multi-platform | Combined with Analytics | Free |

**A/B testing use cases in games:**
- Monetization pricing (different IAP prices per cohort).
- Difficulty tuning (different level curves).
- Progression speed.
- Tutorial variations (compare FTUE designs).
- Ad frequency (impact on cap vs retention vs revenue).

**Process:**
1. Define hypothesis (e.g., "Lower difficulty improves D7 retention by 10%").
2. Create control (A) and variant (B) groups — typical 50/50 split.
3. Run for statistical significance (usually 2-4 weeks for games).
4. Analyze by cohort, region, platform.
5. Implement the winning variant, archive the experiment.

### 7.4 Content Updates Without App Updates

**Unity Addressables System:**

The system allows distributing new content without an app store submission:

1. Assets are packaged in AssetBundles (independent, movable).
2. A remote catalog is downloaded at startup (lists available bundles).
3. Content updates rebuild bundles → only changed assets are downloaded.
4. Automatic catalog switching (old bundles for unchanged assets; new for updated ones).

**Two-tier content structure:**
- **Tier 1 — Does not change post-release**: Static content shipped with the app (few large bundles).
- **Tier 2 — Can change post-release**: Dynamic online content in smaller bundles (updated frequently).

**Example workflow:**
1. Dev updates 2 of 100 level assets.
2. Rebuild bundles → creates a new bundle with 2 assets + updated catalog.
3. Upload to CDN, update manifest URL in the game.
4. Players download the updated catalog → fetch only the 2 new assets.
5. Game reflects changes immediately.

**CDN**: Unity UGS Cloud Content Delivery as official support.

**Advantages**: No app store approval delays, smaller downloads (delta updates), instant rollback (revert the catalog URL), A/B test content by geo/cohort.

---

## 8. Post-Launch

### 8.1 Crash Reporting

| Aspect | Firebase Crashlytics | Sentry | Backtrace |
|--------|---------------------|--------|-----------|
| **Price** | Free | Freemium | Paid (gaming-focused) |
| **Setup** | Easiest | Moderate | Moderate |
| **Crash Coverage** | Excellent | Excellent | Gaming-optimized |
| **Performance Data** | Limited | Robust | Robust |
| **Context Depth** | Basic | Advanced (source maps, release tracking) | Advanced |
| **Best for** | Small teams, starting out | Growing teams | Gaming specialists, console |

**Recommendation by phase:**
- **Starting (< 50k MAU)**: Firebase Crashlytics (free, zero-setup with Firebase).
- **Growth (50k-500k)**: Sentry freemium (deeper debugging, performance monitoring).
- **Scale (> 500k)**: Sentry paid or Backtrace (gaming-specific).

### 8.2 Player Feedback Loops

**Critical practice: Close the loop.**
- Implement player feedback; publicly credit those who suggested it.
- Reject suggestions transparently (technical limitations, design vision, resources, prioritization).
- This practice massively impacts retention and community health.

**Feedback channels:**
- **In-Game**: Buttons in the settings menu for quick surveys.
- **Discord**: Dedicated channel per type of feedback (bugs, features, balance, UI).
- **External surveys**: Post-session forms (optional, with a reward).
- **Analytics**: Use retention/churn cohorts to identify pain points.
- **Social media**: Monitor sentiment on Twitter/TikTok/Reddit.

**Implementation framework:**
1. Define feedback categories (bugs, features, balance, UI).
2. Assign ownership (who reviews which category).
3. Response SLA (acknowledge within X days).
4. Public roadmap showing the impact of feedback.
5. Monthly community updates citing implemented suggestions.

**Balance**: Take feedback seriously, but do not relinquish the creative vision. Handing full control to players = a product nobody wants to play.

### 8.3 Update Cadence

| Game Type | Frequency | Content |
|-----------|-----------|---------|
| **Live Service (F2P)** | Weekly (balance + content) + Seasonal (every 10 weeks) | Balance changes, new content, limited-time events |
| **Single-Player / Episodic** | Monthly | Bug fixes, QoL, DLC timing tied to revenue targets |
| **Casual Mobile** | Bi-weekly to monthly | Balance patches, light seasonal events |
| **Premium PC (indie)** | Monthly (first 3 months) → quarterly | Bug fixes, QoL, content patches, DLC |

**Key data**: Players who see new content in the first week have significantly higher conversion to regular users. Plan the first 3 months of patches and roadmap updates before launch.

**Regional adaptation:**
- Frequency-first regions (e.g., China): Weekly content is expected.
- Depth-first regions (e.g., Western markets): Prefer fewer, higher-quality updates.

---

## 9. Platform Checklists

### ✅ Google Play Store

- [ ] Target API level 35+ (Android 15)
- [ ] Build in AAB format
- [ ] Play Asset Delivery configured (if > 150MB)
- [ ] Privacy policy published and linked
- [ ] Data safety form filled out
- [ ] IARC age rating
- [ ] Screenshots (phone + tablet) optimized for conversion
- [ ] Short description with primary keywords
- [ ] Android Vitals monitored (crash rate < 1%)
- [ ] Internal testing track configured
- [ ] Firebase Crashlytics integrated
- [ ] Staged rollout planned (1% → 5% → 25% → 100%)
- [ ] Deep links configured (if applicable)
- [ ] ProGuard/R8 configured for obfuscation

### ✅ Apple App Store

- [ ] Apple Developer Program ($99/year) active
- [ ] Certificates + Provisioning Profiles configured
- [ ] Minimum SDK iOS/iPadOS 26 (from April 2026)
- [ ] Privacy Nutrition Labels filled out
- [ ] Age rating updated (4+/9+/13+/16+/18+ system)
- [ ] TestFlight configured for beta testing
- [ ] Screenshots for all required device sizes
- [ ] App Review Guidelines verified (especially violence content)
- [ ] In-App Purchase configured in App Store Connect
- [ ] Subscription management (if applicable)
- [ ] ATT (App Tracking Transparency) implemented
- [ ] SKAdNetwork configured

### ✅ Steam

- [ ] Steamworks account ($100 per app)
- [ ] Steamworks.NET integrated in Unity
- [ ] steam_appid.txt configured
- [ ] Store page published (Coming Soon) in advance
- [ ] Capsule art readable at all sizes
- [ ] Trailer with gameplay in the first 5 seconds
- [ ] 20 tags used (specific ones)
- [ ] Short description with genre in the first 10 words
- [ ] Screenshots showing gameplay variety
- [ ] Steam Deck compatibility verified
- [ ] Achievements configured
- [ ] Trading cards (optional, but recommended)
- [ ] Regional pricing configured
- [ ] Priority localizations added
- [ ] Demo/playtest published (optional, but recommended)

### ✅ itch.io (Prototypes/Demos)

- [ ] WebGL build optimized (Brotli or Gzip compression)
- [ ] Game page with screenshots and description
- [ ] Devlog started for community building
- [ ] Relevant tags added
- [ ] Browser embed configured (correct dimensions)
- [ ] Link to Steam wishlist / other platforms (if applicable)

---

## 10. Benchmark Metrics by Genre

### Retention

| Genre | D1 | D7 | D30 | Sessions/Day |
|-------|----|----|-----|-------------|
| **Casual (puzzle, match-3)** | 40%+ | 15%+ | 5%+ | 2-3 |
| **Mid-Core (RPG, strategy)** | 35%+ | 12%+ | 5%+ | 6-7 |
| **Board/Card/Casino** | Best retention in category | High | High | 4-5 |
| **Hyper-Casual** | 30-35% | 5-8% | 1-3% | 3-4 |
| **General median** | 22% | 3.4-3.9% | ~2-3% | 4 |
| **Top 10%** | 40%+ | 12%+ | 10%+ | — |
| **Top 25%** | 26-28% | — | — | — |

### ARPDAU (Average Revenue Per Daily Active User)

| Model | Healthy Range | Top Performers |
|-------|--------------|----------------|
| **Ad-Monetized (Casual/Hyper)** | $0.05-$0.15 | $0.20+ |
| **IAP-Driven (Mid-Core/RPG)** | $0.30-$1.00 | $1.00+ |
| **Hybrid (Ads + IAP)** | $0.25+ | Highest ARPDAU in category |

### Regional Variation (D1 Retention)

| Region | D1 Retention | D30 Retention |
|--------|-------------|---------------|
| **North America** | 30.26% | 3.72% |
| **Japan** | — | 6.4% (~2x US) |

**Critical insight**: Retention is the strongest predictor of long-term revenue.

---

## 11. Recommended Tools

### By Category

| Category | Tool | Rationale | Price |
|----------|------|-----------|-------|
| **CI/CD** | GameCI + GitHub Actions | Open-source, multi-platform, native GitHub integration | Free |
| **CI/CD (alt)** | Unity Build Automation | Managed, minimal setup, good free tier | Free tier + metered |
| **Analytics (starting)** | GameAnalytics | Ready-made game metrics, free up to 10k MAU | Free / $299 Pro |
| **Analytics (scale)** | Firebase Analytics | Completely free, BigQuery export, infinite scale | Free |
| **Crash Reporting** | Firebase Crashlytics | Zero-setup, free, real-time alerts | Free |
| **Crash (advanced)** | Sentry | Deep debugging, performance monitoring | Freemium |
| **Remote Config** | Firebase Remote Config | Free, conditional values, real-time rollback | Free |
| **Feature Flags** | ConfigCat | Gaming-focused, simple | Freemium |
| **A/B Testing** | Firebase + Amplitude | Firebase free for basics; Amplitude for advanced | Free / Freemium |
| **IAP** | Unity IAP v5.0 | Unified Apple/Google, Billing Library v8 compliance | Free (SDK) |
| **Ads** | Unity Ads + AdMob mediation | Rewarded video + interstitials with mediation | Revenue share |
| **Content Delivery** | Unity Addressables + CDN | Delta updates without app store, instant rollback | Free (SDK) + CDN costs |
| **Steam SDK** | Steamworks.NET | Open-source, 100% API coverage, MIT license | Free |
| **Community** | Discord | Direct communication, feedback channels, voting | Free |
| **ASO** | AppTweak / ASOLYTICS | Keyword tracking, competitor analysis | Paid |

### Recommended Stack for Indie Unity (2025-2026)

```
Build:        GameCI + GitHub Actions
Testing:      Unity Test Framework (EditMode + PlayMode)
Analytics:    GameAnalytics (starting) → Firebase Analytics (scale)
Crashes:      Firebase Crashlytics
Remote Config: Firebase Remote Config
IAP:          Unity IAP v5.0
Ads:          Unity Ads + AdMob mediation
Content:      Unity Addressables + Cloud Content Delivery
Steam:        Steamworks.NET
Community:    Discord + itch.io devlog
```

---

## 12. Sources and References

### Build Pipeline & CI/CD
- [Unity Android Build Requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/android-requirements-and-compatibility.html)
- [Unity Build Process for Android](https://docs.unity3d.com/Manual/android-BuildProcess.html)
- [Unity CI/CD Solutions](https://unity.com/solutions/ci-cd)
- [Unity Build Automation Pricing](https://support.unity.com/hc/en-us/articles/6093104438932)
- [Understanding New DevOps Charges (Mar 2026)](https://support.unity.com/hc/en-us/articles/34748492914964)
- [GameCI Official Docs](https://game.ci/docs/github/getting-started/)
- [GameCI GitHub Actions](https://github.com/game-ci/unity-actions)
- [Jenkins Unity3D Plugin](https://plugins.jenkins.io/unity3d-plugin/)
- [IL2CPP Build Size Optimizations](https://support.unity.com/hc/en-us/articles/208412186)
- [Addressables Optimization Q&A — Unity Blog](https://blog.unity.com/engine-platform/extended-q-a-optimizing-memory-and-build-size-with-addressables)
- [500MB to 50MB Reduction Case Study](https://outscal.com/blog/unity-mobile-build-size-optimization)
- [ASTC Texture Compression — ARM Developer](https://developer.arm.com/documentation/100140/0100/optimizations-for-applications-in-unity/gpu-optimizations-in-unity/astc-texture-compression-in-unity)
- [Unity Test Framework Docs](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/index.html)

### Publishing
- [Google Play Review Timeline 2025](https://be-dev.pl/blog/eng/google-play-review-time-2025-how-long-does-it-really-take-to-publish-your-app-on-android)
- [ASO in 2026 Complete Guide](https://asomobile.net/en/blog/aso-in-2026-the-complete-guide-to-app-optimization/)
- [AppTweak ASO Guide 2026](https://www.apptweak.com/en/aso-blog/what-is-app-store-optimization-and-why-is-aso-important)
- [Play Asset Delivery — Unity Manual](https://docs.unity3d.com/Manual/play-asset-delivery.html)
- [Apple Upcoming Requirements](https://developer.apple.com/news/upcoming-requirements/)
- [App Store Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- [TestFlight Overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview/)
- [iOS App Distribution Guide 2026](https://foresightmobile.com/blog/ios-app-distribution-guide-2026)
- [Steamworks.NET](https://steamworks.github.io/)
- [Steam Store Page Optimization Guide](https://presskit.gg/field-guides/steam-page-optimization-guide)
- [Steam Page Best Practices — Indie Game Joe](https://indiegamejoe.com/steam-store-page-optimization-above-the-fold-best-practices)
- [Steam Deck Compatibility Review](https://partner.steamgames.com/doc/steamdeck/compat)

### Soft Launch & Metrics
- [Mobile Game KPI Benchmarks 2026](https://gamegrowthadvisor.com/blog/2026-03-17-mobile-game-kpis-benchmarks-2026/)
- [D1/D7/D30 Retention in Gaming — Solsten](https://solsten.io/blog/d1-d7-d30-retention-in-gaming)
- [Mobile Retention Benchmarks 2026 — InvestGame](https://investgame.net/wp-content/uploads/2026/01/2026-01-20-Mobile_retention_benchmarks_2026.pdf)
- [How to Soft Launch a Mobile Game 2024](https://lancaric.me/how-to-soft-launch-a-mobile-game-in-2024/)
- [Soft Launch Playbook — a16z](https://a16z.com/mobile-game-soft-launch/)
- [New Soft Launch Countries — PocketGamer](https://www.pocketgamer.biz/comment-and-opinion/62110/new-soft-launch-countries/)

### Analytics
- [Unity Analytics Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.services.analytics.html)
- [GameAnalytics Pricing](https://www.gameanalytics.com/pricing)
- [GameAnalytics Event Tracking](https://docs.gameanalytics.com/integrations/sdk/unity/event-tracking)
- [Firebase Analytics Unity Setup](https://firebase.google.com/docs/analytics/unity/get-started)
- [GameAnalytics Funnel Guide](https://www.gameanalytics.com/blog/exploring-gaming-funnels)
- [GameAnalytics Dashboard Guide](https://www.gameanalytics.com/blog/how-to-build-killer-dashboards-in-game-analytics)
- [GameAnalytics 2025 Mobile Gaming Benchmarks](https://www.gameanalytics.com/reports/2025-mobile-gaming-benchmarks)

### Monetization
- [Top Game Monetisation Strategies for Indies](https://www.thegamemarketer.com/insight-posts/top-game-monetisation-strategies-for-indie-developers)
- [Indie Game Monetization 2026 — DEV Community](https://dev.to/linou518/indie-game-monetization-in-2026-premium-dlc-or-subscription-which-path-is-right-for-you-955)
- [How to Price Your Game — Unity](https://unity.com/blog/how-to-price-your-game)
- [Rewarded Video Ads Best Practices — Adjust](https://www.adjust.com/blog/understanding-rewarded-video-ads/)
- [Mobile Game Interstitial Strategies — Airflux](https://airflux.ai/blog/mobile-game-interstitial-strategies)
- [Unity IAP Official Docs](https://docs.unity.com/ugs/en-us/manual/iap/manual/overview)
- [Unity IAP Complete Guide 2025](https://www.voxelbusters.com/blog/unity-iap-complete-guide-2025)

### Live-Ops
- [Firebase Remote Config Docs](https://firebase.google.com/docs/remote-config)
- [Firebase Remote Config Codelab for Games](https://firebase.google.com/codelabs/instrument-your-game-with-firebase-remote-config)
- [Unity Remote Config Manual](https://docs.unity3d.com/Manual/com.unity.remote-config.html)
- [Firebase Remote Config Alternatives — Flagsmith](https://www.flagsmith.com/blog/firebase-remote-config-alternatives)
- [Adaptive Gaming with Feature Flags — ConfigCat](https://configcat.com/blog/2025/02/28/adaptive-gaming-with-feature-flags/)
- [Feature Flag Best Practices 2025](https://www.octopus.com/devops/feature-flags/feature-flag-best-practices/)
- [Addressables Content Update Workflow](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/ContentUpdateWorkflow.html)
- [Addressables Remote Content Distribution](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/RemoteContentDistribution.html)

### Post-Launch
- [Sentry vs Crashlytics Guide](https://sentry.io/resources/sentry-vs-crashlytics-mobile-developers-guide/)
- [Firebase Crashlytics Docs](https://firebase.google.com/docs/crashlytics)
- [Game Community Management Guide 2025](https://generalistprogrammer.com/tutorials/game-community-management-complete-engagement-guide-2025/)
- [Player Feedback Collection Guide](https://indiedevgames.com/igniting-player-feedback-collection-the-essential-guide-to-effective-game-feedback-management/)
- [Why Gaming Updates Matter 2026](https://www.pcmobilegames.com/general/why-gaming-updates-matter-gameplay-community-2026/)
- [Casual Game Report H1 2025 — AppMagic](https://appmagic.rocks/research/casual-report-h1-2025)

---

*Guide compiled in April 2026. Platform tools and requirements can change — always check the official documentation before submitting builds.*
