pub mod commands;
pub mod events;
pub mod types;

pub fn run() {
    tauri::Builder::default()
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