pub mod commands;
pub mod events;
pub mod node_supervisor;
pub mod types;

use tauri::{Manager, WindowEvent};

use node_supervisor::NodeSupervisor;

pub fn run() {
    tauri::Builder::default()
        .manage(NodeSupervisor::new())
        .setup(|app| {
            let app_handle = app.handle().clone();
            tauri::async_runtime::spawn(async move {
                let supervisor = app_handle.state::<NodeSupervisor>();
                match supervisor.spawn().await {
                    Ok(pid) => println!("[node-supervisor] spawned PID {pid}"),
                    Err(e) => eprintln!("[node-supervisor] spawn failed: {e}"),
                }
            });
            Ok(())
        })
        .on_window_event(|window, event| {
            if let WindowEvent::CloseRequested { api, .. } = event {
                // Prevent the default close so we can shut the child down
                // before the process exits. Without this, tokio's
                // kill_on_drop is the only safety net — works, but logs are
                // noisier and timing is fuzzier.
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
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}