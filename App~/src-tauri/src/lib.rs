//! Tauri application entry point.
//!
//! Wires up shared state (`NodeSupervisor`, `UnityClient`), spawns background
//! workers during `setup`, intercepts the window close event for a graceful
//! shutdown, and registers every Tauri command exposed to the frontend.

// region: Module declarations

pub mod commands;
pub mod events;
pub mod node_supervisor;
pub mod types;
pub mod unity_client;

// endregion

use tauri::{AppHandle, Manager, WindowEvent};

use node_supervisor::NodeSupervisor;
use unity_client::UnityClient;

// region: Single-instance handler

/// Argument prefix that re-launches use to request a route change in the
/// already-running window — see [`handle_single_instance`].
const ROUTE_ARG_PREFIX: &str = "--route=";

/// Single-instance callback fired when a second invocation is detected by the
/// plugin while the primary window is still alive.
///
/// Focuses + unminimizes the existing window and, when the new invocation
/// carries a `--route=/path` CLI argument, emits the `route-requested` event
/// so the React side can navigate the running window.
fn handle_single_instance(app: &AppHandle, args: Vec<String>, _cwd: String) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.unminimize();
        let _ = window.set_focus();
    }

    let route = args
        .iter()
        .find_map(|arg| arg.strip_prefix(ROUTE_ARG_PREFIX).map(|s| s.to_string()));

    if let Some(route) = route {
        if let Err(e) =
            events::emit_route_requested(app, types::RouteRequestedPayload { route })
        {
            eprintln!("[single-instance] failed to emit route-requested: {e}");
        }
    }
}

// endregion

// region: Application bootstrap

/// Builds and runs the Tauri application.
///
/// Registers `NodeSupervisor` and `UnityClient` as managed state, spawns the
/// Node SDK child process and the Unity client worker during setup, intercepts
/// `CloseRequested` for graceful shutdown, and binds every IPC command exposed
/// to the React frontend.
///
/// Blocks until the application exits. Panics if the Tauri runtime fails to
/// start (e.g. invalid `tauri.conf.json`).
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(handle_single_instance))
        .plugin(tauri_plugin_cli::init())
        .plugin(tauri_plugin_opener::init())
        .manage(NodeSupervisor::new())
        .manage(UnityClient::new())
        .setup(|app| {
            let app_handle = app.handle().clone();

            let app_for_node = app_handle.clone();
            tauri::async_runtime::spawn(async move {
                let supervisor = app_for_node.state::<NodeSupervisor>();
                match supervisor.spawn(app_for_node.clone()).await {
                    Ok(pid) => println!("[node-supervisor] spawned PID {pid}"),
                    Err(e) => eprintln!("[node-supervisor] spawn failed: {e}"),
                }
            });

            // Unity client — connect, heartbeat, reconnect with backoff.
            let unity = app_handle.state::<UnityClient>();
            unity.start(app_handle.clone());

            Ok(())
        })
        .on_window_event(|window, event| {
            if let WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let app = window.app_handle().clone();
                tauri::async_runtime::spawn(async move {
                    if let Some(supervisor) = app.try_state::<NodeSupervisor>() {
                        supervisor.shutdown().await;
                    }
                    app.exit(0);
                });
            }
        })
        .invoke_handler(tauri::generate_handler![
            commands::connection::get_unity_status,
            commands::connection::get_node_sdk_status,
            commands::connection::reconnect_unity,
            commands::connection::restart_node_sdk,
            commands::conversation::send_message,
            commands::conversation::get_conversation_history,
            commands::conversation::clear_conversation,
            commands::conversation::set_permission_mode,
            commands::conversation::get_permission_mode,
            commands::plans::list_plans,
            commands::plans::read_plan,
            commands::plans::write_plan,
            commands::plans::delete_plan,
            commands::rules::list_rules,
            commands::rules::read_rule,
            commands::rules::write_rule,
            commands::rules::delete_rule,
            commands::rules::toggle_rule,
            commands::settings::get_settings,
            commands::settings::update_settings,
            commands::dev::dev_emit_test_event,
            commands::dev::node_ping,
            commands::dev::dev_call_unity_tool,
            commands::env::get_env_var,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

// endregion