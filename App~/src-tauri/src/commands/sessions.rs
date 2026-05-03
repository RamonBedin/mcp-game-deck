//! Session list + history loading + resume control commands.
//!
//! Reads Claude Code's per-project session storage at
//! `<home>/.claude/projects/<encoded-cwd>/<id>.jsonl` (Decision #6 —
//! their storage is the source of truth, not ours). Each command runs
//! synchronously on Tauri's command thread pool; JSONL files are tens
//! of KB to a few MB and the parsing cost is dwarfed by serializing
//! the result back to React.

use std::fs;
use std::path::PathBuf;

use serde_json::Value;
use tauri::State;

use crate::claude_supervisor::{paths, ClaudeSupervisor};
use crate::types::{
    AppError, LoadedBlock, LoadedMessage, MessageRole, SessionSummary,
};

// region: Constants

/// Maximum number of characters surfaced as a session title — beyond
/// this length the sidebar gets noisy. Computed from the first user
/// prompt's leading non-empty line.
const TITLE_MAX_CHARS: usize = 60;

// endregion

// region: Tauri commands

/// Lists every Claude Code session stored under the active Unity
/// project's encoded directory. Sorted by `last_modified` descending —
/// newest first. Empty when the directory doesn't exist yet (fresh
/// install).
///
/// # Errors
///
/// Returns `AppError::Internal` when `UNITY_PROJECT_PATH` is unset,
/// the home directory can't be resolved, or directory iteration fails.
#[tauri::command]
pub fn get_sessions() -> Result<Vec<SessionSummary>, AppError> {
    let project_path = resolve_project_path()?;
    let dir = match paths::claude_sessions_dir(&project_path) {
        Some(p) => p,
        None => {
            return Err(AppError::Internal(
                "could not resolve home directory (USERPROFILE/HOME unset)".into(),
            ));
        }
    };

    if !dir.is_dir() {
        return Ok(Vec::new());
    }

    let mut summaries = Vec::new();
    let entries = fs::read_dir(&dir)
        .map_err(|e| AppError::Internal(format!("read_dir {}: {e}", dir.display())))?;

    for entry in entries.flatten() {
        let path = entry.path();
        if path.extension().and_then(|s| s.to_str()) != Some("jsonl") {
            continue;
        }
        let id = match path.file_stem().and_then(|s| s.to_str()) {
            Some(s) => s.to_string(),
            None => continue,
        };
        match summarize_session(&path, &id) {
            Ok(summary) => summaries.push(summary),
            Err(e) => {
                eprintln!(
                    "[sessions] failed to summarize {}: {e}",
                    path.display()
                );
            }
        }
    }

    summaries.sort_by(|a, b| b.last_modified.cmp(&a.last_modified));
    Ok(summaries)
}

/// Loads the full message history for one session as React-shaped
/// blocks. Tool-result user lines are folded into the preceding
/// assistant turn so the rendered chat matches what the live stream
/// builds via `appendDelta` / `appendToolUseBlock` /
/// `appendToolResultBlock`.
///
/// # Errors
///
/// Returns `AppError::FileNotFound` when the JSONL is missing or
/// `AppError::Internal` for I/O / encoding failures.
#[tauri::command]
pub fn get_session_messages(session_id: String) -> Result<Vec<LoadedMessage>, AppError> {
    let project_path = resolve_project_path()?;
    let dir = paths::claude_sessions_dir(&project_path)
        .ok_or_else(|| AppError::Internal("home directory unresolvable".into()))?;
    let file = dir.join(format!("{session_id}.jsonl"));

    if !file.is_file() {
        return Err(AppError::FileNotFound(file.display().to_string()));
    }

    let raw = fs::read_to_string(&file)
        .map_err(|e| AppError::Internal(format!("read {}: {e}", file.display())))?;

    Ok(load_messages(&raw))
}

/// Pins a session id so the next prompt resumes that conversation.
/// React clears its local message list immediately and pre-populates
/// it with `get_session_messages` output before invoking this command.
///
/// # Errors
///
/// `AppError::Internal` when the supervisor can't push the control
/// message (writer closed, encoding failure).
#[tauri::command]
pub async fn resume_session(
    session_id: String,
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .set_resume_session(session_id)
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
}

/// Resets the supervisor back to a fresh session — the next prompt
/// starts a new JSONL file under Claude Code's storage.
///
/// # Errors
///
/// `AppError::Internal` when the supervisor can't push the control
/// message.
#[tauri::command]
pub async fn start_new_session(
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .clear_resume_session()
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
}

// endregion

// region: Internal — project path

/// Reads `UNITY_PROJECT_PATH` from the environment. The launch
/// contract from Feature 07 guarantees this on a healthy spawn — when
/// it's missing, surfacing an error is the correct behavior.
fn resolve_project_path() -> Result<String, AppError> {
    std::env::var("UNITY_PROJECT_PATH")
        .ok()
        .filter(|s| !s.is_empty())
        .ok_or_else(|| {
            AppError::Internal("UNITY_PROJECT_PATH env var not set".into())
        })
}

// endregion

// region: Internal — summary

/// Summarizes one JSONL file: derives the title from the first user
/// prompt, counts user+assistant entries, and pulls the last-modified
/// timestamp from the file's mtime.
fn summarize_session(path: &PathBuf, id: &str) -> std::io::Result<SessionSummary> {
    let metadata = fs::metadata(path)?;
    let last_modified = metadata
        .modified()
        .ok()
        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0);

    let raw = fs::read_to_string(path)?;
    let mut title = format!("Session {}", id.chars().take(8).collect::<String>());
    let mut message_count = 0usize;

    for line in raw.lines() {
        let value: Value = match serde_json::from_str(line) {
            Ok(v) => v,
            Err(_) => continue,
        };
        let line_type = value.get("type").and_then(|v| v.as_str());
        match line_type {
            Some("user") | Some("assistant") => {
                message_count += 1;
                if title.starts_with("Session ") && line_type == Some("user") {
                    if let Some(extracted) = extract_user_title(&value) {
                        title = extracted;
                    }
                }
            }
            _ => continue,
        }
    }

    Ok(SessionSummary {
        id: id.to_string(),
        title,
        last_modified,
        message_count,
    })
}

/// Pulls the first text block out of a user line's `message.content`.
/// Returns `None` for user lines whose only content is `tool_result`
/// blocks (auto-generated SDK echoes don't make useful titles).
fn extract_user_title(line: &Value) -> Option<String> {
    let content = line.pointer("/message/content")?;
    let raw = match content {
        Value::String(s) => s.clone(),
        Value::Array(blocks) => {
            let mut out = String::new();
            for block in blocks {
                if block.get("type").and_then(|v| v.as_str()) == Some("text") {
                    if let Some(t) = block.get("text").and_then(|v| v.as_str()) {
                        out.push_str(t);
                        break;
                    }
                }
            }
            if out.is_empty() {
                return None;
            }
            out
        }
        _ => return None,
    };

    Some(clean_title(&raw))
}

/// Strips the `<command-message>` wrapper Claude Code occasionally
/// adds on init prompts, then takes the first non-empty line, trims
/// leading markdown heading markers, and clamps to `TITLE_MAX_CHARS`.
fn clean_title(raw: &str) -> String {
    let stripped = strip_command_wrapper(raw);
    let line = stripped
        .lines()
        .map(|l| l.trim())
        .find(|l| !l.is_empty())
        .unwrap_or("");
    let cleaned = line.trim_start_matches('#').trim();
    truncate_with_ellipsis(cleaned, TITLE_MAX_CHARS)
}

/// Removes a leading `<command-message ...>...</command-message>`
/// envelope if present. Defensive — most prompts don't carry one.
fn strip_command_wrapper(text: &str) -> String {
    let trimmed = text.trim_start();
    if !trimmed.starts_with("<command-message") {
        return text.to_string();
    }
    let after_open = match trimmed.find('>') {
        Some(idx) => &trimmed[idx + 1..],
        None => return text.to_string(),
    };
    match after_open.find("</command-message>") {
        Some(idx) => {
            let mut result = after_open[..idx].to_string();
            result.push_str(&after_open[idx + "</command-message>".len()..]);
            result
        }
        None => after_open.to_string(),
    }
}

/// Truncates `text` to `max` characters, appending `…` when cut.
/// Operates on chars (not bytes) so multi-byte UTF-8 stays valid.
fn truncate_with_ellipsis(text: &str, max: usize) -> String {
    let chars: Vec<char> = text.chars().collect();
    if chars.len() <= max {
        return text.to_string();
    }
    let mut out: String = chars.into_iter().take(max).collect();
    out.push('…');
    out
}

// endregion

// region: Internal — message loading

/// Walks the raw JSONL string and produces React-shaped messages.
/// User lines that contain only `tool_result` blocks are folded into
/// the previous assistant message — matches what `appendDelta` /
/// `appendToolResultBlock` build during live streaming.
fn load_messages(raw: &str) -> Vec<LoadedMessage> {
    let mut messages: Vec<LoadedMessage> = Vec::new();

    for line in raw.lines() {
        let value: Value = match serde_json::from_str(line) {
            Ok(v) => v,
            Err(_) => continue,
        };
        let line_type = match value.get("type").and_then(|v| v.as_str()) {
            Some(s) => s,
            None => continue,
        };

        match line_type {
            "user" => append_user_line(&mut messages, &value),
            "assistant" => append_assistant_line(&mut messages, &value),
            _ => continue,
        }
    }

    messages
}

/// Folds one `type:"user"` JSONL line into `messages`. Lines that
/// are a real user prompt (string content or any text block) become a
/// new `MessageRole::User` entry; lines made entirely of
/// `tool_result` blocks extend the trailing assistant turn instead.
fn append_user_line(messages: &mut Vec<LoadedMessage>, line: &Value) {
    let content = match line.pointer("/message/content") {
        Some(c) => c,
        None => return,
    };
    let id = read_string(line, "uuid").unwrap_or_else(|| make_local_id("user"));
    let timestamp = parse_timestamp(line);

    match content {
        Value::String(text) => {
            let cleaned = strip_command_wrapper(text);
            if cleaned.trim().is_empty() {
                return;
            }
            messages.push(LoadedMessage {
                id,
                role: MessageRole::User,
                timestamp,
                blocks: vec![LoadedBlock::Text { text: cleaned }],
            });
        }
        Value::Array(blocks) => {
            let mut text_blocks: Vec<LoadedBlock> = Vec::new();
            let mut tool_results: Vec<LoadedBlock> = Vec::new();
            for block in blocks {
                let kind = block.get("type").and_then(|v| v.as_str()).unwrap_or("");
                match kind {
                    "text" => {
                        if let Some(t) = block.get("text").and_then(|v| v.as_str()) {
                            text_blocks.push(LoadedBlock::Text {
                                text: strip_command_wrapper(t),
                            });
                        }
                    }
                    "tool_result" => {
                        let tool_use_id = read_string(block, "tool_use_id")
                            .unwrap_or_default();
                        let result_content = block
                            .get("content")
                            .cloned()
                            .unwrap_or(Value::Null);
                        let is_error = block
                            .get("is_error")
                            .and_then(|v| v.as_bool())
                            .unwrap_or(false);
                        tool_results.push(LoadedBlock::ToolResult {
                            tool_use_id,
                            content: result_content,
                            is_error,
                        });
                    }
                    _ => continue,
                }
            }

            if !text_blocks.is_empty() {
                let mut blocks = text_blocks;
                blocks.extend(tool_results);
                messages.push(LoadedMessage {
                    id,
                    role: MessageRole::User,
                    timestamp,
                    blocks,
                });
            } else if !tool_results.is_empty() {
                if let Some(last) = messages.last_mut() {
                    if last.role == MessageRole::Assistant {
                        last.blocks.extend(tool_results);
                        return;
                    }
                }
                // Orphan tool_result with no preceding assistant — rare,
                // surface as a system entry so it isn't silently dropped.
                messages.push(LoadedMessage {
                    id,
                    role: MessageRole::System,
                    timestamp,
                    blocks: tool_results,
                });
            }
        }
        _ => {}
    }
}

/// Folds one `type:"assistant"` line into `messages`. Each assistant
/// line carries a `message.content` array of `text` / `tool_use` /
/// `thinking` blocks; thinking is dropped, the rest are appended to
/// the trailing assistant turn (or start a new one if the previous
/// message was a user prompt).
fn append_assistant_line(messages: &mut Vec<LoadedMessage>, line: &Value) {
    let content = match line.pointer("/message/content").and_then(|v| v.as_array()) {
        Some(arr) => arr,
        None => return,
    };

    let mut new_blocks: Vec<LoadedBlock> = Vec::new();
    for block in content {
        let kind = block.get("type").and_then(|v| v.as_str()).unwrap_or("");
        match kind {
            "text" => {
                if let Some(t) = block.get("text").and_then(|v| v.as_str()) {
                    new_blocks.push(LoadedBlock::Text { text: t.to_string() });
                }
            }
            "tool_use" => {
                let tool_use_id = read_string(block, "id").unwrap_or_default();
                let name = read_string(block, "name").unwrap_or_default();
                let input = block.get("input").cloned().unwrap_or(Value::Null);
                new_blocks.push(LoadedBlock::ToolUse {
                    tool_use_id,
                    name,
                    input,
                });
            }
            // thinking + anything else: skip
            _ => continue,
        }
    }

    if new_blocks.is_empty() {
        return;
    }

    if let Some(last) = messages.last_mut() {
        if last.role == MessageRole::Assistant {
            last.blocks.extend(new_blocks);
            return;
        }
    }

    let id = read_string(line, "uuid").unwrap_or_else(|| make_local_id("asst"));
    let timestamp = parse_timestamp(line);
    messages.push(LoadedMessage {
        id,
        role: MessageRole::Assistant,
        timestamp,
        blocks: new_blocks,
    });
}

/// Reads `field` from `value` as an owned string, returning `None`
/// when missing or non-string.
fn read_string(value: &Value, field: &str) -> Option<String> {
    value.get(field).and_then(|v| v.as_str()).map(String::from)
}

/// Parses the JSONL line's ISO-8601 `timestamp` into milliseconds
/// since epoch via a tiny manual scan — avoids pulling `chrono` for
/// one field. Returns `0` when missing or unparseable; React handles
/// `0` by hiding the timestamp display.
fn parse_timestamp(line: &Value) -> i64 {
    let s = match line.get("timestamp").and_then(|v| v.as_str()) {
        Some(s) => s,
        None => return 0,
    };
    parse_iso8601_millis(s).unwrap_or(0)
}

/// Best-effort ISO-8601 parser for the format Claude Code uses
/// (`YYYY-MM-DDTHH:MM:SS.sssZ`). Returns milliseconds since epoch.
fn parse_iso8601_millis(s: &str) -> Option<i64> {
    // Format: 2026-04-16T00:30:35.773Z
    let bytes = s.as_bytes();
    if bytes.len() < 20 {
        return None;
    }
    let year: i64 = s.get(0..4)?.parse().ok()?;
    let month: i64 = s.get(5..7)?.parse().ok()?;
    let day: i64 = s.get(8..10)?.parse().ok()?;
    let hour: i64 = s.get(11..13)?.parse().ok()?;
    let minute: i64 = s.get(14..16)?.parse().ok()?;
    let second: i64 = s.get(17..19)?.parse().ok()?;

    // Optional fractional seconds (e.g., ".773")
    let mut millis: i64 = 0;
    if let Some(dot_idx) = s.find('.') {
        let after_dot = &s[dot_idx + 1..];
        let frac_end = after_dot
            .find(|c: char| !c.is_ascii_digit())
            .unwrap_or(after_dot.len());
        let frac = &after_dot[..frac_end.min(3)];
        if !frac.is_empty() {
            millis = frac.parse::<i64>().ok()?;
            // pad to 3 digits
            for _ in frac.len()..3 {
                millis *= 10;
            }
        }
    }

    let days = days_from_civil(year, month, day);
    let total_seconds = days * 86_400 + hour * 3600 + minute * 60 + second;
    Some(total_seconds * 1000 + millis)
}

/// Days from the proleptic Gregorian epoch (1970-01-01) using the
/// civil_from_days inverse from Howard Hinnant's date algorithms.
/// Avoids pulling `chrono` for a single timestamp parse.
fn days_from_civil(y: i64, m: i64, d: i64) -> i64 {
    let y = if m <= 2 { y - 1 } else { y };
    let era = if y >= 0 { y } else { y - 399 } / 400;
    let yoe = y - era * 400;
    let doy = (153 * (if m > 2 { m - 3 } else { m + 9 }) + 2) / 5 + d - 1;
    let doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
    era * 146_097 + doe - 719_468
}

/// Builds a stable id when the JSONL line is missing `uuid`.
/// Mirrors React's `makeLocalId` shape so the React side can treat
/// loaded ids the same as live-stream ids.
fn make_local_id(prefix: &str) -> String {
    use std::time::{SystemTime, UNIX_EPOCH};
    let nanos = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.subsec_nanos())
        .unwrap_or(0);
    format!("{prefix}-loaded-{nanos:x}")
}

// endregion
