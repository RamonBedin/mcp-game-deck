//! Tauri command handlers exposed to the React frontend.
//!
//! Each submodule groups commands by domain (connection, conversation,
//! plans, rules, settings, dev). All commands are registered in
//! `lib.rs::run` via `tauri::generate_handler!`. Stubs here will be filled
//! in by later Feature work; today they keep the typed surface honest so
//! the frontend can wire its calls.

pub mod connection;
pub mod conversation;
pub mod dev;
pub mod env;
pub mod plans;
pub mod rules;
pub mod settings;