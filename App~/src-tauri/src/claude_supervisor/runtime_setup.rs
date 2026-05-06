//! Generates `App~/runtime/sdk-entry.js` on first launch (or when the
//! file goes missing). Separate module from `sdk_install.rs` because
//! the responsibilities differ: sdk_install owns the one-time `npm
//! install` operation; runtime_setup owns idempotent file generation
//! that runs every spawn.
//!
//! Production packaging will likely embed the entry script in the

use crate::claude_supervisor::paths;

// region: Constants

/// Source of `sdk-entry.js` written verbatim. Kept as `include_str!`
/// rather than a Rust raw string so the editor highlights JS syntax
/// at edit time. The .js sibling file is part of the supervisor
/// crate, not shipped as a standalone asset.
const SDK_ENTRY_SCRIPT: &str = include_str!("sdk_entry.js");

// endregion

// region: Public surface

/// Writes `App~/runtime/sdk-entry.js` if it doesn't exist. No-op
/// otherwise — does NOT overwrite an existing file (so users can
/// hand-edit during local debugging without a relaunch surprising
/// them).
///
/// Defensively creates the parent directory if missing. The runtime
/// dir is normally created by `sdk_install::prepare_runtime_dir` on
/// first launch, but `ensure_entry_script` runs from `mod.rs::spawn`
/// before the install flow completes (e.g., on a fresh clone where
/// the user hasn't gone through the FirstRunPanel yet), so this
/// guard lets the call succeed regardless of setup ordering.
///
/// `package.json` "type": "module" presence is `sdk_install.rs`'s
/// job; this module only writes the entry script.
///
/// # Errors
///
/// Returns `std::io::Error` when the write fails.
pub async fn ensure_entry_script() -> std::io::Result<()> {
    let path = paths::sdk_entry_script();
    if tokio::fs::metadata(&path).await.is_ok() {
        return Ok(());
    }
    if let Some(parent) = path.parent() {
        tokio::fs::create_dir_all(parent).await?;
    }
    tokio::fs::write(&path, SDK_ENTRY_SCRIPT).await
}

// endregion