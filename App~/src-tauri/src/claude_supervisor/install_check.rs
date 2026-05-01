//! Detection probes for the local Claude Code install + auth + SDK presence.
//!
//! Powers the FirstRunPanel (task 1.2) by answering four independent
//! questions in a single async call:
//!
//! 1. Is `claude` resolvable on PATH? — `where.exe claude` (Windows)
//! 2. What version is installed? — parsed from `claude --version`
//! 3. Is the user authenticated? — parsed from `claude /status`
//! 4. Is `@anthropic-ai/claude-agent-sdk` installed? — package.json
//!    presence at the runtime path used by the supervisor (task 2.2)
//!
//! Each probe is fault-tolerant: failures yield `false` / `None` for the
//! affected field rather than propagating, since the React panel treats
//! "missing" and "probe broken" the same way.

use std::path::PathBuf;
use std::process::Stdio;
use std::time::Duration;

use tokio::process::Command;
use tokio::time::timeout;

use crate::types::ClaudeInstallStatus;

// region: Constants

/// Maximum time we wait for any single subprocess probe before declaring it
/// failed. `claude /status` is the slow path (~500ms-1s when authenticated
/// on a warm machine; can hang longer in pathological configs). Five seconds
/// keeps the FirstRunPanel poll responsive without falsely failing on a
/// busy machine.
const PROBE_TIMEOUT: Duration = Duration::from_secs(5);

// endregion

// region: Public surface

/// Bundles the four detection probes into a single async call.
///
/// Probes run in parallel via `tokio::join!`; total latency is bounded by
/// the slowest probe (typically `claude /status`). Each probe is
/// self-contained — a failure in one does not affect the others.
///
/// # Returns
///
/// A `ClaudeInstallStatus` populated with the probe results. No call site
/// needs to handle errors: failures are folded into `false` / `None`.
pub async fn check_install_status() -> ClaudeInstallStatus {
    let (claude_installed, claude_version, claude_authenticated, sdk_installed) = tokio::join!(
        detect_claude_installed(),
        detect_claude_version(),
        detect_claude_authenticated(),
        detect_sdk_installed(),
    );

    ClaudeInstallStatus {
        claude_installed,
        claude_authenticated,
        sdk_installed,
        claude_version,
    }
}

// endregion

// region: claude binary

/// True when `claude` resolves on PATH.
///
/// Windows: `where.exe claude` exits 0 when the binary is found, 1 when not.
/// Other platforms: best-effort via `which claude`. Spawn errors map to
/// `false` (e.g. lookup tool not present, OS denies the spawn).
async fn detect_claude_installed() -> bool {
    let mut cmd = if cfg!(windows) {
        let mut c = Command::new("where.exe");
        c.arg("claude");
        c
    } else {
        let mut c = Command::new("which");
        c.arg("claude");
        c
    };

    cmd.stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null());

    match timeout(PROBE_TIMEOUT, cmd.status()).await {
        Ok(Ok(status)) => status.success(),
        Ok(Err(e)) => {
            eprintln!("[install-check] claude lookup failed to spawn: {e}");
            false
        }
        Err(_) => {
            eprintln!("[install-check] claude lookup timed out");
            false
        }
    }
}

// endregion

// region: claude version

/// Parses the version string from `claude --version`.
///
/// Expected output is line-formatted like `Claude Code 2.10.3 (Anthropic)`;
/// the parser extracts the first whitespace-separated token shaped like a
/// dotted numeric version (with optional `v` prefix). Returns `None` when
/// the binary is missing, the call times out, or no token matches.
///
/// Windows: invoked via `cmd /C claude --version` because npm-installed
/// `claude` is a `.cmd` shim that `Command::new("claude")` won't resolve
/// (the Rust-side PATHEXT lookup only finds `.exe`).
async fn detect_claude_version() -> Option<String> {
    let mut cmd = if cfg!(windows) {
        let mut c = Command::new("cmd");
        c.args(["/C", "claude", "--version"]);
        c
    } else {
        let mut c = Command::new("claude");
        c.arg("--version");
        c
    };
    cmd.stdin(Stdio::null()).stderr(Stdio::null());

    let output = timeout(PROBE_TIMEOUT, cmd.output()).await;

    let stdout = match output {
        Ok(Ok(out)) if out.status.success() => out.stdout,
        Ok(Ok(_)) => return None,
        Ok(Err(e)) => {
            eprintln!("[install-check] `claude --version` failed to spawn: {e}");
            return None;
        }
        Err(_) => {
            eprintln!("[install-check] `claude --version` timed out");
            return None;
        }
    };

    let text = String::from_utf8_lossy(&stdout);
    parse_version(&text)
}

/// Extracts the first dotted-numeric token (e.g. `2.10.3`) from `text`.
///
/// Tolerates a leading `v` and requires at least two numeric segments
/// separated by `.`. Returns the version without the `v` prefix.
fn parse_version(text: &str) -> Option<String> {
    text.split_whitespace()
        .find(|tok| {
            let stripped = tok.trim_start_matches('v');
            let parts: Vec<&str> = stripped.split('.').collect();
            parts.len() >= 2
                && parts
                    .iter()
                    .all(|p| !p.is_empty() && p.chars().all(|c| c.is_ascii_digit()))
        })
        .map(|tok| tok.trim_start_matches('v').to_string())
}

// endregion

// region: claude auth

/// True when the local `claude` install reports an authenticated session.
///
/// Spawns `claude /status` with stdin closed so the CLI cannot block on
/// interactive input, then parses stdout for known "logged out" phrasing.
/// The probe is best-effort: at the time of writing, Claude Code does not
/// document a stable machine-readable auth check (spec calls this out
/// explicitly under task 1.1 — "verify in docs at task time"). Heuristics:
///
/// - Spawn fails / timeout / non-zero exit → `false`
/// - Output contains a known "not logged in" / "please log in" phrase → `false`
/// - Otherwise (output produced, exit 0, no negative phrase) → `true`
///
/// A future Claude Code release that changes the wording without changing
/// the exit code would silently flip this to `true`. That is acceptable
/// here: authentication failures will surface again on the first real
/// query during spawn (task 2.2 health check), so this probe is only the
/// fast path for the FirstRunPanel UX.
async fn detect_claude_authenticated() -> bool {
    let mut cmd = if cfg!(windows) {
        let mut c = Command::new("cmd");
        c.args(["/C", "claude", "/status"]);
        c
    } else {
        let mut c = Command::new("claude");
        c.arg("/status");
        c
    };
    cmd.stdin(Stdio::null()).stderr(Stdio::null());

    let output = timeout(PROBE_TIMEOUT, cmd.output()).await;

    let out = match output {
        Ok(Ok(out)) if out.status.success() => out,
        Ok(Ok(_)) => return false,
        Ok(Err(e)) => {
            eprintln!("[install-check] `claude /status` failed to spawn: {e}");
            return false;
        }
        Err(_) => {
            eprintln!("[install-check] `claude /status` timed out");
            return false;
        }
    };

    let text = String::from_utf8_lossy(&out.stdout).to_lowercase();
    const NEGATIVE_MARKERS: &[&str] = &[
        "not logged in",
        "please log in",
        "please run `claude /login`",
        "not authenticated",
    ];
    !NEGATIVE_MARKERS.iter().any(|m| text.contains(m))
}

// endregion

// region: SDK package

/// True when `@anthropic-ai/claude-agent-sdk`'s `package.json` exists at the
/// runtime path the supervisor will read in task 2.2.
///
/// Path is anchored at `CARGO_MANIFEST_DIR` (= `App~/src-tauri/` at compile
/// time), walked up one level to `App~/`, then joined with the runtime
/// subtree. Same dev-only resolution caveat as
/// `node_supervisor::spawn::resolve_stub_script`: production binary
/// placement may need a different anchor; that is a Feature 02 task 7.x
/// concern, not a 1.1 one.
async fn detect_sdk_installed() -> bool {
    let manifest = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    let app_dir = match manifest.parent() {
        Some(dir) => dir.to_path_buf(),
        None => {
            eprintln!("[install-check] could not derive App~ dir from manifest");
            return false;
        }
    };

    let sdk_path = app_dir
        .join("runtime")
        .join("node_modules")
        .join("@anthropic-ai")
        .join("claude-agent-sdk")
        .join("package.json");

    tokio::fs::metadata(&sdk_path)
        .await
        .map(|m| m.is_file())
        .unwrap_or(false)
}

// endregion

// region: Tests

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_version_extracts_dotted_token() {
        assert_eq!(
            parse_version("Claude Code 2.10.3 (Anthropic)"),
            Some("2.10.3".to_string())
        );
    }

    #[test]
    fn parse_version_handles_v_prefix() {
        assert_eq!(parse_version("claude v1.0.5"), Some("1.0.5".to_string()));
    }

    #[test]
    fn parse_version_returns_none_when_no_match() {
        assert_eq!(parse_version("Claude Code (build unknown)"), None);
    }

    #[test]
    fn parse_version_picks_first_match() {
        assert_eq!(parse_version("foo 2.0.0 bar 3.1.4"), Some("2.0.0".to_string()));
    }
}

// endregion
