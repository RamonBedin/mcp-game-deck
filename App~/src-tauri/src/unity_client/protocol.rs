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

// Caps the response read for a POST. The C# server's MAX_REQUEST_BODY_SIZE is
// 16 MiB — responses are typically far smaller, but tool payloads (e.g. log
// dumps) can grow. 4 MiB is a comfortable safety margin for dev.
const MAX_POST_RESPONSE_BYTES: usize = 4 * 1024 * 1024;

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

/// Sends an HTTP POST with Bearer auth + JSON body, returns
/// `(status_code, response_body)`. Reads until the server closes the
/// connection (we send `Connection: close`).
pub async fn http_post_json(
    addr: SocketAddr,
    body: &str,
    auth_token: &str,
    t: Duration,
) -> std::io::Result<(u16, String)> {
    let attempt = async {
        let mut stream = TcpStream::connect(addr).await?;

        let request = format!(
            "POST / HTTP/1.1\r\n\
             Host: localhost\r\n\
             Authorization: Bearer {auth_token}\r\n\
             Content-Type: application/json\r\n\
             Content-Length: {len}\r\n\
             Connection: close\r\n\
             \r\n\
             {body}",
            len = body.len(),
        );
        stream.write_all(request.as_bytes()).await?;
        stream.flush().await?;

        // Read until EOF — server closes after responding (we sent Connection: close).
        let mut buf = Vec::with_capacity(8192);
        let mut chunk = [0u8; 8192];
        loop {
            let n = stream.read(&mut chunk).await?;
            if n == 0 {
                break;
            }
            buf.extend_from_slice(&chunk[..n]);
            if buf.len() > MAX_POST_RESPONSE_BYTES {
                return Err(std::io::Error::new(
                    std::io::ErrorKind::Other,
                    "response exceeds MAX_POST_RESPONSE_BYTES",
                ));
            }
        }

        let raw = std::str::from_utf8(&buf).map_err(|e| {
            std::io::Error::new(
                std::io::ErrorKind::InvalidData,
                format!("response not valid UTF-8: {e}"),
            )
        })?;

        let header_end = raw.find("\r\n\r\n").ok_or_else(|| {
            std::io::Error::new(
                std::io::ErrorKind::InvalidData,
                "response missing CRLFCRLF header terminator",
            )
        })?;
        let header_str = &raw[..header_end];
        let body_str = &raw[header_end + 4..];

        let status_line = header_str.lines().next().ok_or_else(|| {
            std::io::Error::new(std::io::ErrorKind::InvalidData, "empty response")
        })?;
        let status_code = status_line
            .split_whitespace()
            .nth(1)
            .and_then(|s| s.parse::<u16>().ok())
            .ok_or_else(|| {
                std::io::Error::new(
                    std::io::ErrorKind::InvalidData,
                    "could not parse HTTP status code",
                )
            })?;

        Ok::<(u16, String), std::io::Error>((status_code, body_str.to_string()))
    };

    match timeout(t, attempt).await {
        Ok(result) => result,
        Err(_) => Err(std::io::Error::new(
            std::io::ErrorKind::TimedOut,
            "request timed out",
        )),
    }
}