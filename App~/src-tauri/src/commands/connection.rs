//! Connection-status Tauri commands.
//!
//! Surface the live state of the Unity TCP client and the Node Agent SDK
//! supervisor to the React frontend, plus manual reconnect / restart hooks.

use tauri::{AppHandle, State};

use crate::claude_supervisor::ClaudeSupervisor;
use crate::files_watcher::FilesWatcher;
use crate::plans_watcher::PlansWatcher;
use crate::rules_watcher::RulesWatcher;
use crate::types::{AppError, ConnectionStatus, SupervisorStatus};
use crate::unity_client::UnityClient;

// region: Status getters

/// Reads the current Unity TCP connection status from `UnityClient`.
///
/// Sync — microsecond cheap. Polled every 2s by `App.tsx`; the
/// `unity-status-changed` event provides the fast-path updates between polls.
///
/// # Arguments
///
/// * `client` - Tauri-managed `UnityClient` state.
///
/// # Returns
///
/// The latest `ConnectionStatus` observed by the run loop.
#[tauri::command]
pub fn get_unity_status(client: State<'_, UnityClient>) -> ConnectionStatus {
    client.current_status()
}

/// Reads the live state machine maintained by the Claude Code supervisor.
///
/// Sync — microsecond cheap. Polled every 2s by `App.tsx`; the
/// `supervisor-status-changed` event provides the fast-path updates
/// between polls.
///
/// # Arguments
///
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Returns
///
/// The latest `SupervisorStatus` (Idle / Starting / Ready / Crashed /
/// Failed).
#[tauri::command]
pub fn get_supervisor_status(supervisor: State<'_, ClaudeSupervisor>) -> SupervisorStatus {
    supervisor.current_status()
}

// endregion

// region: Manual triggers

/// Nudges the Unity TCP client to re-probe the MCP server immediately,
/// bypassing any in-progress reconnect backoff.
///
/// This is the manual recovery path for a stuck "disconnected" (red)
/// state — the status dot in the UI calls it on click, and it is also
/// the hook the agent-facing control MCP uses to recover the editor
/// connection without restarting the whole app. Returns immediately;
/// the actual re-probe happens asynchronously on the run loop and the
/// result surfaces via the usual `unity-status-changed` event.
///
/// # Arguments
///
/// * `client` - Tauri-managed `UnityClient` state.
#[tauri::command]
pub fn reconnect_unity(client: State<'_, UnityClient>) {
    client.request_reconnect();
}

/// Restarts the Claude Code supervisor and rebinds the plans + files
/// watchers (plus the rules watcher) and refreshes the rules bundle.
///
/// `UNITY_PROJECT_PATH` may have changed between launches (the env is
/// read by `claude_supervisor::spawn` on every call), so all three
/// watchers are restarted so they pick up the new project's
/// directories. The rules bundle (`Library/MCPGameDeck/rules-bundle.md`)
/// is also recomposed before the supervisor spawns so its first
/// `query()` injects the correct project's rules. Each watcher's
/// `start` internally stops any existing task before spawning the
/// new one, so this is safe to call repeatedly.
///
/// # Arguments
///
/// * `app` - Application handle forwarded to `spawn` and both watchers.
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
/// * `watcher` - Tauri-managed `PlansWatcher` state.
/// * `files_watcher` - Tauri-managed `FilesWatcher` state.
/// * `rules_watcher` - Tauri-managed `RulesWatcher` state.
///
/// # Errors
///
/// Returns `AppError::Internal` when `spawn` fails. Watcher rebind
/// errors are logged on stderr but not surfaced — every watcher will
/// retry internally and a failure here shouldn't block the user from
/// restarting the supervisor.
#[tauri::command]
pub async fn restart_supervisor(
    app: AppHandle,
    supervisor: State<'_, ClaudeSupervisor>,
    watcher: State<'_, PlansWatcher>,
    files_watcher: State<'_, FilesWatcher>,
    rules_watcher: State<'_, RulesWatcher>,
) -> Result<(), AppError> {
    if let Some(root) = crate::project_root::try_resolve_project_root() {
        if let Err(e) = crate::rules_bundle::recompose(&root) {
            eprintln!("[rules-bundle] restart_supervisor recompose failed: {e}");
        }
    }
    supervisor
        .spawn(app.clone())
        .await
        .map(|_pid| ())
        .map_err(|e| AppError::Internal(e.to_string()))?;
    watcher.start(app.clone()).await;
    files_watcher.start(app.clone()).await;
    rules_watcher.start(app).await;
    Ok(())
}

// endregion