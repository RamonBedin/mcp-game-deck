//! Connect / heartbeat / reconnect orchestration.
//!
//! Strategy:
//! - Heartbeat = HTTP GET `/` with a 2s connect+request timeout.
//! - Connected: heartbeat every HEARTBEAT_INTERVAL until one fails.
//! - Disconnected: backoff schedule [1s, 2s, 5s, 10s, 30s] capped, reset on
//!   successful reconnect — avoids tight-loop reconnects when Unity is down.

use std::net::SocketAddr;
use std::time::Duration;

use super::protocol::http_get_status;

// region: Schedule

/// Interval between heartbeats while connected.
pub const HEARTBEAT_INTERVAL: Duration = Duration::from_secs(5);

/// Per-attempt timeout used by `heartbeat` (covers TCP connect + request).
pub const CONNECT_TIMEOUT: Duration = Duration::from_secs(2);

/// Backoff delays in seconds, indexed by attempt count and capped at the
/// last entry.
pub const BACKOFF_SCHEDULE_SECS: &[u64] = &[1, 2, 5, 10, 30];

// endregion

// region: Probes

/// Probes Unity's MCP server with a single HTTP GET `/`.
///
/// # Arguments
///
/// * `addr` - Address of the Unity MCP server.
///
/// # Returns
///
/// `true` iff the server responded with HTTP 200 within `CONNECT_TIMEOUT`.
pub async fn heartbeat(addr: SocketAddr) -> bool {
    matches!(http_get_status(addr, CONNECT_TIMEOUT).await, Ok(200))
}

/// Returns the backoff delay for a given reconnect attempt.
///
/// # Arguments
///
/// * `attempt` - Zero-based attempt index. Indices past the schedule's last
///   entry clamp to that entry.
///
/// # Returns
///
/// The corresponding `Duration` from `BACKOFF_SCHEDULE_SECS`.
pub fn backoff_delay(attempt: usize) -> Duration {
    let i = attempt.min(BACKOFF_SCHEDULE_SECS.len() - 1);
    Duration::from_secs(BACKOFF_SCHEDULE_SECS[i])
}

// endregion