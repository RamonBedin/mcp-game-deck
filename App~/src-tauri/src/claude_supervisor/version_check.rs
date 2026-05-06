//! Compares the local `claude --version` against the smoke-tested
//! version range and surfaces a non-blocking warning to React when
//! the detected build falls outside it.
//!
//! Triggered as a fire-and-forget task from `lib.rs::run`'s setup
//! closure, in parallel with `ClaudeSupervisor::spawn`. The check is
//! advisory only: out-of-range versions do not block startup, do not
//! transition the supervisor to a failure state, and do not retry —
//! the user gets a one-shot, dismissable banner and the app keeps
//! running on whatever version is installed.
//!
//! Source of truth for the supported range is `package.json`'s
//! `claudeCode` field (repo root). The constants below mirror it by
//! convention; bumping support means editing both. Drift is
//! self-correcting on the next time anyone touches the range, and the
//! worst-case symptom of divergence is a slightly inaccurate banner.

use std::time::Duration;

use tauri::AppHandle;

use crate::claude_supervisor::install_check;
use crate::events::emit_claude_version_out_of_range;
use crate::types::ClaudeVersionOutOfRangePayload;

// region: Constants

const MIN_VERSION: (u32, u32, u32) = (2, 1, 0);
const MAX_VERSION_EXCL: (u32, u32, u32) = (3, 0, 0);
const EMIT_DELAY: Duration = Duration::from_secs(6);

fn supported_range_string() -> String {
    let (a, b, c) = MIN_VERSION;
    let (d, e, f) = MAX_VERSION_EXCL;
    format!(">={a}.{b}.{c} <{d}.{e}.{f}")
}

// endregion

// region: Public surface

/// Probes `claude --version`, compares against the supported range
/// and emits `claude-version-out-of-range` once when the detected
/// build falls outside it.
///
/// Silent in three cases — none of them warrant pestering the user:
///
/// * No version detected (`claude` missing, probe timed out): the
///   FirstRunPanel already covers the missing-binary surface.
/// * Version unparseable (e.g. an exotic build string we don't
///   recognise): better to stay quiet than fire a false positive.
/// * Version inside the range: silence is success.
pub async fn run(app: AppHandle) {
    let detected_str = match install_check::detect_claude_version().await {
        Some(v) => v,
        None => return,
    };
    let detected = match parse_version(&detected_str) {
        Some(v) => v,
        None => {
            eprintln!(
                "[version-check] could not parse claude version '{detected_str}'; skipping range check"
            );
            return;
        }
    };

    if detected >= MIN_VERSION && detected < MAX_VERSION_EXCL {
        return;
    }

    let supported = supported_range_string();
    eprintln!(
        "[version-check] claude {detected_str} outside supported range {supported}; surfacing banner in {:?}",
        EMIT_DELAY
    );
    tokio::time::sleep(EMIT_DELAY).await;
    let _ = emit_claude_version_out_of_range(
        &app,
        ClaudeVersionOutOfRangePayload {
            detected: detected_str,
            supported,
        },
    );
}

// endregion

// region: Parser

/// Parses a version string of the form `[v]MAJOR.MINOR[.PATCH][-pre][+build]`
/// into a `(major, minor, patch)` tuple. Missing patch is normalized
/// to `0`. Anything after `-` or `+` is discarded — pre-release and
/// build metadata don't affect the smoke-tested range.
fn parse_version(raw: &str) -> Option<(u32, u32, u32)> {
    let cleaned = raw.trim().trim_start_matches('v');
    let core = cleaned
        .split(|c| c == '-' || c == '+')
        .next()
        .unwrap_or(cleaned);

    let mut parts = core.split('.');
    let major = parts.next()?.parse::<u32>().ok()?;
    let minor = parts.next()?.parse::<u32>().ok()?;
    let patch = match parts.next() {
        Some(s) => s.parse::<u32>().ok()?,
        None => 0,
    };
    Some((major, minor, patch))
}

// endregion

// region: Tests

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_simple() {
        assert_eq!(parse_version("2.1.128"), Some((2, 1, 128)));
    }

    #[test]
    fn parse_v_prefix() {
        assert_eq!(parse_version("v1.0.5"), Some((1, 0, 5)));
    }

    #[test]
    fn parse_two_segments_pads_patch() {
        assert_eq!(parse_version("2.10"), Some((2, 10, 0)));
    }

    #[test]
    fn parse_strips_prerelease_suffix() {
        assert_eq!(parse_version("2.10.3-beta.1"), Some((2, 10, 3)));
    }

    #[test]
    fn parse_strips_build_metadata() {
        assert_eq!(parse_version("2.10.3+build.42"), Some((2, 10, 3)));
    }

    #[test]
    fn parse_rejects_garbage() {
        assert_eq!(parse_version("abc"), None);
        assert_eq!(parse_version("2.x"), None);
        assert_eq!(parse_version(""), None);
    }

    #[test]
    fn range_lower_bound_inclusive() {
        let v = parse_version("2.1.0").unwrap();
        assert!(v >= MIN_VERSION && v < MAX_VERSION_EXCL);
    }

    #[test]
    fn range_below_lower_bound_excluded() {
        let v = parse_version("2.0.99").unwrap();
        assert!(!(v >= MIN_VERSION && v < MAX_VERSION_EXCL));
    }

    #[test]
    fn range_upper_bound_exclusive() {
        let v = parse_version("3.0.0").unwrap();
        assert!(!(v >= MIN_VERSION && v < MAX_VERSION_EXCL));
    }

    #[test]
    fn range_just_below_upper_bound_in_range() {
        let v = parse_version("2.99.99").unwrap();
        assert!(v >= MIN_VERSION && v < MAX_VERSION_EXCL);
    }

    #[test]
    fn range_local_dev_version_in_range() {
        let v = parse_version("2.1.128").unwrap();
        assert!(v >= MIN_VERSION && v < MAX_VERSION_EXCL);
    }
}

// endregion