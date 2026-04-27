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

pub const HEARTBEAT_INTERVAL: Duration = Duration::from_secs(5);
pub const CONNECT_TIMEOUT: Duration = Duration::from_secs(2);
pub const BACKOFF_SCHEDULE_SECS: &[u64] = &[1, 2, 5, 10, 30];

/// Returns true iff Unity's MCP server replies 200 OK to GET `/`.
pub async fn heartbeat(addr: SocketAddr) -> bool {
    matches!(http_get_status(addr, CONNECT_TIMEOUT).await, Ok(200))
}

/// Returns the backoff delay for the given attempt index, capped at the
/// last entry of the schedule.
pub fn backoff_delay(attempt: usize) -> Duration {
    let i = attempt.min(BACKOFF_SCHEDULE_SECS.len() - 1);
    Duration::from_secs(BACKOFF_SCHEDULE_SECS[i])
}