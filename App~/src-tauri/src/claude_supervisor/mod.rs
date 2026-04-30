//! Claude Code supervisor module — owns the Claude Code subprocess that
//! powers the chat experience.
//!
//! Currently only exposes install detection (Feature 02 task 1.1). The full
//! supervisor state machine (spawn / shutdown / status) lands in task 2.1
//! and replaces `node_supervisor` at that point.

pub mod install_check;
pub mod paths;
pub mod sdk_install;