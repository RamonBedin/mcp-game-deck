//! Minimal, dependency-free file logger for the Tauri host.
//!
//! Release builds compile with `windows_subsystem = "windows"` (see
//! `main.rs`), so the process has **no console**: every `println!` /
//! `eprintln!` writes to an invalid stdout/stderr handle. At best the
//! output is discarded; at worst the write errors and the `print!`
//! family **panics**, silently killing the spawning task (this is how
//! the Unity reconnect loop could die without a trace). That made the
//! whole connection layer impossible to debug from a shipped binary.
//!
//! This module gives us a durable log at
//! `<unity-project>/Library/GameDeck/app.log` plus a panic hook, so a
//! panicking background task leaves a record instead of vanishing.
//!
//! Every write here uses `write_all(...).ok()` — which returns an
//! `io::Result` we deliberately ignore — rather than the `print!`
//! macros, so logging itself can never panic even when both std handles
//! are dead.

use std::fs::{File, OpenOptions};
use std::io::Write;
use std::path::PathBuf;
use std::sync::{Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};

// region: State

static LOG_FILE: OnceLock<Mutex<Option<File>>> = OnceLock::new();

fn cell() -> &'static Mutex<Option<File>> {
    LOG_FILE.get_or_init(|| Mutex::new(None))
}

// endregion

// region: Init

/// Resolves `<project>/Library/GameDeck/app.log`, creating parent dirs.
/// Returns `None` when no project root is known yet or the directory
/// can't be created.
fn resolve_log_path() -> Option<PathBuf> {
    let root = crate::project_root::try_resolve_project_root()?;
    let dir = root.join("Library").join("GameDeck");
    if std::fs::create_dir_all(&dir).is_err() {
        return None;
    }
    Some(dir.join("app.log"))
}

/// Opens the log file (append mode) and installs a panic hook that
/// records panics to the log before delegating to the default hook.
///
/// Call once during `setup`, after the environment (and thus
/// `UNITY_PROJECT_PATH`) is available. Safe to call again — it simply
/// reopens the file handle.
pub fn init() {
    if let Some(path) = resolve_log_path() {
        if let Ok(file) = OpenOptions::new().create(true).append(true).open(&path) {
            if let Ok(mut guard) = cell().lock() {
                *guard = Some(file);
            }
        }
    }

    let previous = std::panic::take_hook();
    std::panic::set_hook(Box::new(move |info| {
        log("PANIC", &format!("{info}"));
        previous(info);
    }));

    log("INFO", "-------- logging initialized --------");
}

// endregion

// region: Log

/// Appends a timestamped line to the log file and mirrors it to stderr.
/// Never panics — all write errors are swallowed.
pub fn log(level: &str, msg: &str) {
    let line = format!("{} [{level}] {msg}\n", now_utc());

    // Direct handle write (NOT eprintln!) so a dead stderr can't panic.
    let _ = std::io::stderr().write_all(line.as_bytes());

    if let Ok(mut guard) = cell().lock() {
        if let Some(file) = guard.as_mut() {
            let _ = file.write_all(line.as_bytes());
            let _ = file.flush();
        }
    }
}

// endregion

// region: Timestamp

/// Formats the current time as `YYYY-MM-DD HH:MM:SS.mmmZ` (UTC).
fn now_utc() -> String {
    let ms = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as u64)
        .unwrap_or(0);

    let secs = ms / 1000;
    let millis = ms % 1000;
    let days = (secs / 86_400) as i64;
    let tod = secs % 86_400;
    let (h, mi, s) = (tod / 3600, (tod % 3600) / 60, tod % 60);
    let (y, mo, d) = civil_from_days(days);

    format!("{y:04}-{mo:02}-{d:02} {h:02}:{mi:02}:{s:02}.{millis:03}Z")
}

/// Converts days-since-Unix-epoch to a `(year, month, day)` civil date.
/// Howard Hinnant's `civil_from_days` algorithm (public domain).
fn civil_from_days(z: i64) -> (i64, u32, u32) {
    let z = z + 719_468;
    let era = if z >= 0 { z } else { z - 146_096 } / 146_097;
    let doe = (z - era * 146_097) as i64; // [0, 146096]
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365; // [0, 399]
    let y = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100); // [0, 365]
    let mp = (5 * doy + 2) / 153; // [0, 11]
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32; // [1, 31]
    let m = if mp < 10 { mp + 3 } else { mp - 9 } as u32; // [1, 12]
    (if m <= 2 { y + 1 } else { y }, m, d)
}

// endregion