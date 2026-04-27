//! Minimal HTTP/1.1 client helpers for talking to Unity's MCP server.
//!
//! Unity's `Editor/MCP/Server/McpServer.cs` speaks HTTP/1.1 over a `TcpListener`
//! (despite the "WebSocket" naming in some C# comments). Each request is a
//! standalone HTTP message — keep-alive is supported but we open a fresh
//! connection per call for simplicity.
//!
//! Wire shape:
//! - **GET /** (heartbeat) — no auth, returns 200 + `{"status":"ok"}`.
//! - **POST /** (MCP RPC) — Bearer auth required, JSON-RPC 2.0 body.
//!   Wired in task 4.2 once we know the Unity project path (needed to read
//!   the auth token at `<project>/Library/GameDeck/auth-token`).

use std::net::SocketAddr;
use std::time::Duration;

use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::TcpStream;
use tokio::time::timeout;

const HTTP_GET_REQUEST: &[u8] =
    b"GET / HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n";

/// Sends an HTTP GET to `/` and returns the parsed status code.
/// Wrapped in a connect+request timeout so a hung server can't stall caller.
pub async fn http_get_status(addr: SocketAddr, t: Duration) -> std::io::Result<u16> {
    let attempt = async {
        let mut stream = TcpStream::connect(addr).await?;
        stream.write_all(HTTP_GET_REQUEST).await?;

        // The status line fits comfortably in 64 bytes:
        // "HTTP/1.1 200 OK\r\n..."
        let mut buf = [0u8; 64];
        let n = stream.read(&mut buf).await?;
        if n == 0 {
            return Err(std::io::Error::new(
                std::io::ErrorKind::UnexpectedEof,
                "empty response",
            ));
        }

        let prefix = std::str::from_utf8(&buf[..n]).unwrap_or("");
        // Status line: HTTP/<version> <code> <reason>
        let code = prefix
            .split_whitespace()
            .nth(1)
            .and_then(|s| s.parse::<u16>().ok())
            .ok_or_else(|| {
                std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    "could not parse HTTP status line",
                )
            })?;

        Ok::<u16, std::io::Error>(code)
    };

    match timeout(t, attempt).await {
        Ok(result) => result,
        Err(_) => Err(std::io::Error::new(
            std::io::ErrorKind::TimedOut,
            "heartbeat timed out",
        )),
    }
}