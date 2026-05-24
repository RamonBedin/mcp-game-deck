//! Windows-specific spawn hygiene applied to the Node child that
//! runs `sdk-entry.js`. Pulled into its own module so the
//! cross-platform spawn path in `spawn.rs` stays platform-agnostic
//! and additional Windows-only knobs (console code page, locale env,
//! terminal mode) can land here without spreading `#[cfg(windows)]`
//! through unrelated call sites.
//!
//! The single concern today is `CREATE_NEW_PROCESS_GROUP`: it puts
//! the Node child in its own Win32 process group, which decouples it
//! from `CTRL_C_EVENT` broadcasts the OS sends to every process
//! sharing the parent's console group. Without it, pressing Ctrl+C
//! in the PowerShell that launched `cargo tauri dev` propagates to
//! Node + the `claude` grandchild and tears the supervisor down even
//! though the user only meant to interrupt the dev script.
//!
//! The flag has no observable effect on packaged builds (Tauri
//! launches without a console parent in production), so it's safe to
//! apply unconditionally on Windows.

use tokio::process::Command;

// region: Constants

#[cfg(windows)]
const CREATE_NEW_PROCESS_GROUP: u32 = 0x0000_0200;

// endregion

// region: Public surface

/// Applies platform-specific spawn hygiene to `cmd`. Currently sets
/// `CREATE_NEW_PROCESS_GROUP` on Windows; no-op on other platforms.
///
/// Call this on the `Command` builder before `spawn()` — the flag
/// only takes effect at process creation.
pub fn apply(cmd: &mut Command) {
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        cmd.as_std_mut().creation_flags(CREATE_NEW_PROCESS_GROUP);
    }
    #[cfg(not(windows))]
    {
        let _ = cmd;
    }
}

// endregion