//! On-demand `npm install` of `@anthropic-ai/claude-agent-sdk`.
//!
//! Drives the FirstRunPanel "installing-sdk" surface. Spawns
//! `npm install @anthropic-ai/claude-agent-sdk` against the
//! Tauri-managed Node runtime, streams stdout to React as
//! `sdk-install-progress` events, and emits `sdk-install-completed`
//! or `sdk-install-failed` on exit.
//!
//! The SDK version is resolved by the npm registry at install time

use std::process::Stdio;
use std::sync::atomic::{AtomicBool, Ordering};

use tauri::AppHandle;
use tokio::io::{AsyncBufReadExt, BufReader};
use tokio::process::Command;

use crate::claude_supervisor::paths;
use crate::events::{
    emit_sdk_install_completed, emit_sdk_install_failed, emit_sdk_install_progress,
};
use crate::types::{SdkInstallFailedPayload, SdkInstallProgressPayload};

// region: Constants

/// Minimal `package.json` written before the first `npm install` runs,
/// keeping npm from auto-generating a manifest with a derived
/// directory name. `"type": "module"` is required for `sdk-entry.js`'s
/// ESM `import` syntax. No `dependencies` field — `npm install <pkg>`
/// adds it with the version chosen by the registry.
const RUNTIME_PACKAGE_JSON: &str = r#"{
  "name": "mcp-game-deck-runtime",
  "version": "0.1.0",
  "private": true,
  "type": "module"
}
"#;

/// Number of stderr lines retained for the failure payload. npm error
/// output is verbose; the last few lines almost always carry the
/// actionable cause.
const STDERR_TAIL: usize = 5;

// endregion

// region: In-flight guard

/// Single-process flag preventing two `npm install` runs from
/// overlapping. The Tauri command sets this on entry and clears it
/// when the spawned task completes.
static INSTALL_IN_FLIGHT: AtomicBool = AtomicBool::new(false);

/// Tries to claim the install slot. Returns `true` when claimed,
/// `false` when another install is already running.
pub fn try_claim_slot() -> bool {
    INSTALL_IN_FLIGHT
        .compare_exchange(false, true, Ordering::SeqCst, Ordering::SeqCst)
        .is_ok()
}

/// Releases the in-flight flag. Safe to call regardless of
/// success/failure.
pub fn release_slot() {
    INSTALL_IN_FLIGHT.store(false, Ordering::SeqCst);
}

// endregion

// region: Public surface

/// Returns true when the SDK is already present on disk. Checked by
/// the Tauri command before claiming the in-flight slot to short-
/// circuit re-installs.
pub async fn is_sdk_installed() -> bool {
    let sdk_path = paths::sdk_package_json();
    tokio::fs::metadata(&sdk_path)
        .await
        .map(|m| m.is_file())
        .unwrap_or(false)
}

/// Runs the install end-to-end: prepares the runtime directory,
/// spawns `npm install`, streams progress, and emits
/// completion/failure events.
///
/// Intended to be called from a `tauri::async_runtime::spawn`'d task.
/// Never panics on npm failures — those become `sdk-install-failed`
/// events. Filesystem errors during preparation also become failure
/// events with an internal message.
pub async fn install_sdk_async(app: AppHandle) {
    if let Err(err) = prepare_runtime_dir().await {
        let _ = emit_sdk_install_failed(
            &app,
            SdkInstallFailedPayload {
                message: format!("failed to prepare runtime dir: {err}"),
                exit_code: None,
            },
        );
        return;
    }

    match run_npm_install(&app).await {
        Ok(()) => {
            let _ = emit_sdk_install_completed(&app);
        }
        Err((message, exit_code)) => {
            let _ = emit_sdk_install_failed(
                &app,
                SdkInstallFailedPayload { message, exit_code },
            );
        }
    }
}

// endregion

// region: Internal — runtime dir prep

/// Ensures `App~/runtime/` exists with a `package.json` that has
/// `"type": "module"` set.
///
/// Three cases:
/// 1. File missing → write `RUNTIME_PACKAGE_JSON` verbatim.
/// 2. File present + `"type": "module"` → no-op.
/// 3. File present + `"type"` missing/different → migrate in-place
///
/// Malformed JSON is left untouched (defensive — `runtime/` is dev-
/// editable and we don't want to clobber a user's hand-edit).
async fn prepare_runtime_dir() -> Result<(), std::io::Error> {
    let runtime = paths::runtime_dir();
    tokio::fs::create_dir_all(&runtime).await?;

    let pkg_json = paths::runtime_package_json();
    let existing = match tokio::fs::read_to_string(&pkg_json).await {
        Ok(s) => s,
        Err(_) => {
            tokio::fs::write(&pkg_json, RUNTIME_PACKAGE_JSON).await?;
            return Ok(());
        }
    };

    let mut value: serde_json::Value = match serde_json::from_str(&existing) {
        Ok(v) => v,
        Err(_) => return Ok(()),
    };

    let obj = match value.as_object_mut() {
        Some(o) => o,
        None => return Ok(()),
    };

    let already_module = obj.get("type").and_then(|v| v.as_str()) == Some("module");
    if already_module {
        return Ok(());
    }

    obj.insert(
        "type".to_string(),
        serde_json::Value::String("module".to_string()),
    );
    let serialized = serde_json::to_string_pretty(&value)
        .map_err(|e| std::io::Error::new(std::io::ErrorKind::Other, e))?;
    tokio::fs::write(&pkg_json, format!("{serialized}\n")).await?;
    Ok(())
}

// endregion

// region: Internal — npm spawn + stream

/// Spawns `npm install @anthropic-ai/claude-agent-sdk`, streams stdout
/// as progress events, retains the last `STDERR_TAIL` stderr lines
/// for failure reporting.
///
/// Returns `Ok(())` on exit code 0; `Err((message, exit_code))` on
/// non-zero exit, spawn failure, or signal termination.
async fn run_npm_install(app: &AppHandle) -> Result<(), (String, Option<i32>)> {
    let runtime_dir = paths::runtime_dir();

    let mut cmd = if cfg!(windows) {
        let mut c = Command::new("cmd");
        c.args(["/C", "npm", "install", "@anthropic-ai/claude-agent-sdk"]);
        c
    } else {
        let mut c = Command::new("npm");
        c.args(["install", "@anthropic-ai/claude-agent-sdk"]);
        c
    };

    cmd.current_dir(&runtime_dir)
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());

    let mut child = match cmd.spawn() {
        Ok(c) => c,
        Err(e) => return Err((format!("npm spawn failed: {e}"), None)),
    };

    let stdout = child.stdout.take().expect("stdout piped");
    let stderr = child.stderr.take().expect("stderr piped");

    let app_for_stdout = app.clone();
    let stdout_task = tokio::spawn(async move {
        let mut reader = BufReader::new(stdout).lines();
        while let Ok(Some(line)) = reader.next_line().await {
            let _ = emit_sdk_install_progress(
                &app_for_stdout,
                SdkInstallProgressPayload {
                    percent: None,
                    message: Some(line),
                },
            );
        }
    });

    let stderr_task = tokio::spawn(async move {
        let mut reader = BufReader::new(stderr).lines();
        let mut tail: Vec<String> = Vec::with_capacity(STDERR_TAIL);
        while let Ok(Some(line)) = reader.next_line().await {
            eprintln!("[sdk-install] {line}");
            if tail.len() == STDERR_TAIL {
                tail.remove(0);
            }
            tail.push(line);
        }
        tail
    });

    let exit_status = match child.wait().await {
        Ok(s) => s,
        Err(e) => return Err((format!("npm wait failed: {e}"), None)),
    };
    let _ = stdout_task.await;
    let stderr_tail = stderr_task.await.unwrap_or_default();

    if exit_status.success() {
        Ok(())
    } else {
        let code = exit_status.code();
        let message = if stderr_tail.is_empty() {
            format!("npm install exited with code {:?}", code)
        } else {
            stderr_tail.join("\n")
        };
        Err((message, code))
    }
}

// endregion