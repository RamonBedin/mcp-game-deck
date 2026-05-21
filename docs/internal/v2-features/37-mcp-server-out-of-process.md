# Feature 37 — MCP Server Out-of-Process

## Status

`proposed` — design pending Ramon approval. Companion specs (`37-mcp-server-out-of-process-spec.md` + `37-mcp-server-out-of-process-tasks.md`) will follow when execution starts.

## Problem

Today's MCP TCP server lives inside Unity's editor AppDomain ([Editor/MCP/Server/McpServer.cs](../../Editor/MCP/Server/McpServer.cs)). Every C# recompile triggers a full AppDomain reload, which destroys and recreates the listener. That coupling produces three concrete pain points:

**(a) Orphan listener accumulation on some machines ([[KI-012]]).** `listener.Stop()` schedules `closesocket()` but `.NET`'s `SafeHandle` defers the actual OS release until the IOCP cancellation for the in-flight `AcceptTcpClientAsync` overlapped I/O unwinds. On hardware where that unwind takes longer than the new AppDomain's `StartServer` takes to run, both listeners coexist briefly — and the OS routes new TCP connects non-deterministically. Tauri's heartbeat lands on the orphan, times out, and never recovers. Restart Unity is the only fix because process death is the only OS-level guarantee that the orphan socket is released. Multiple in-process mitigation attempts (`SO_REUSEADDR` toggles, async accept with cancellation, client-tracking drain on stop, HTTP keep-alive on the client) failed to eliminate the race because we can't synchronize Unity's domain unload with .NET's deferred socket cleanup from managed code.

**(b) Visible reload churn even on unaffected machines.** Even when the orphan doesn't accumulate, every recompile causes:
- Brief disconnection in Tauri's status indicator (red flicker before reconnect).
- In-flight MCP tool calls cancelled mid-execution — the user sees their last action abandoned.
- Proxy reconnect overhead — `tools/list` re-fetch is ~157KB, takes a moment to re-parse + re-register on the SDK side.
- Auth-token re-read on the proxy (cheap, but it's part of the cycle).

**(c) Architectural fragility.** Any future addition to the in-process server (resources, prompts, longer-running operations) inherits the same domain-reload churn. The current model puts a network surface that needs uptime stability inside a runtime designed to reload at will.

## Proposal

Move the public-facing TCP listener out of Unity's AppDomain into a long-lived **sidecar process** spawned and managed by Unity. The sidecar owns `127.0.0.1:8090`, accepts connections from Tauri / `mcp-proxy.js` / future MCP clients, and forwards each JSON-RPC request to Unity over a reload-tolerant IPC channel (named pipe / Unix domain socket). When Unity reloads, the IPC channel from Unity's side momentarily disconnects and reconnects by name; the sidecar holds incoming requests during that window and replays them once Unity is back. External clients see a stable connection that never flickers.

```
Before (today):
  Tauri / Claude Code ──TCP:8090──> Unity (in-process, listener dies on reload)

After (F37):
  Tauri / Claude Code ──TCP:8090──> Sidecar ──IPC──> Unity
                                      └ stable          └ reconnects on reload
```

The sidecar speaks the **same TCP/HTTP JSON-RPC wire format** as the current C# server, so Tauri's `unity_client` and `mcp-proxy.js` need no changes. The shift is invisible from outside.

## Scope IN

- **Sidecar binary.** Small process, exposed surface limited to: TCP accept on `:8090`, JSON-RPC parse, auth-bearer check, IPC forward, JSON-RPC reply. No business logic, no Unity-specific knowledge of tools. ~500 LoC target.
- **Unity-side IPC server.** Listener on a named pipe / Unix socket whose name is derived from project path (so multi-Unity-instance setups don't collide). `[InitializeOnLoad]` static ctor brings it up; `beforeAssemblyReload` tears it down cleanly; new AppDomain's static ctor reopens it. The sidecar reconnects to the new pipe by name — sub-second.
- **Spawn lifecycle.** Unity's `[InitializeOnLoad]` starts the sidecar on first editor load. Unity tracks the sidecar PID and ensures it dies with Unity (Windows Job Object / Unix process group — same pattern as F22 plans for the supervisor). Sidecar respawned automatically if it crashes (exponential backoff).
- **Auth token continuity.** Token is still written to `<project>/Library/GameDeck/auth-token` by Unity. Sidecar reads it at startup and on IPC reconnect (re-read if changed). External clients keep authenticating with the same bearer — no protocol change.
- **Request queueing during reload window.** When Unity-side IPC drops (during reload), sidecar queues incoming TCP requests up to a small bound (~50). When IPC reconnects, queued requests are forwarded. If queue overflows or IPC stays down >10s, sidecar returns `503` to clients so they can apply their own retry. External-facing TCP connections themselves remain open.
- **Failure modes:**
  - Sidecar crash → Unity respawns within 1s, queued client connections close gracefully.
  - Unity-side IPC crash without recompile → sidecar holds connections, waits with backoff.
  - Unity full quit → sidecar detects via IPC EOF + Job Object teardown, exits cleanly.
  - User force-kills Unity (Task Manager) → Job Object kills sidecar atomically.
- **Logging.** Sidecar writes rotating log to `Library/MCPGameDeck/logs/sidecar.log`. Existing Unity-side `McpLogger` keeps logging tool execution to the Unity Console.
- **Backwards compat.**
  - `Editor/Tools/` C# code unchanged — tools still register and dispatch via Unity main thread.
  - `[McpTool]` attribute discovery + `MainThreadDispatcher` flow unchanged.
  - Tauri `unity_client/protocol.rs` unchanged — same `:8090` HTTP target.
  - `Server~/src/mcp-proxy.ts` unchanged — same proxy semantics.

## Scope OUT (deferred to v2.x+)

- **Cross-Unity sidecar sharing.** Each Unity instance gets its own sidecar with its own port (derived per-project, F25-aligned). Sharing one sidecar across multiple Unity Editors is a future optimization once per-project window isolation is solid.
- **Replacing the TCP wire format with UDS-only.** Public surface stays TCP for compatibility with `mcp-proxy.js` and any future external MCP client. UDS is only the internal Unity↔Sidecar channel.
- **Sidecar as a generic MCP server for non-Unity clients.** Focus is Tauri + Claude Code via proxy. Other clients work transparently through the same TCP surface but aren't a deliberate target.
- **Hot reload of sidecar on package update.** Sidecar restarts on package upgrade or Unity restart, not mid-session.
- **In-process telemetry of cross-process tracing.** Latency instrumentation is desirable but coupled with F33 (analytics dashboard) infrastructure — wait for that.
- **Migration of resources / prompts handlers out of Unity.** Tools require `MainThreadDispatcher`; some resources may not, but moving them to the sidecar is a separate optimization for a later cycle.

## Dependencies

- **F22 (Process Hardening)** — should land first or in parallel. F37 adds another managed child process to the lifecycle; it directly benefits from F22's Job Object / process group / auto-restart / graceful shutdown plumbing. If F22 is in flight, F37 plugs into the same infrastructure with minimal duplication.
- **F18 (Package Tooling Fixes)** — independent in behavior, but the sidecar binary becomes part of the Unity package install footprint and interacts with the package-* tools indirectly.

## Risks

- **Two-process complexity.** Another binary to ship, another lifecycle to manage, another channel to debug. Mitigation: sidecar is intentionally small (no tool registry, no auth logic beyond bearer check, no Unity model knowledge). It's a transparent forwarder.
- **IPC choice trade-offs.** Named pipes (Windows-native, well-supported in `System.IO.Pipes`) vs Unix domain sockets (cross-platform via .NET 6+). `NamedPipeServerStream` works on both Windows and Unix in modern .NET — leans toward that. Decision finalized in spec phase after a small bench of latency + reconnect behavior.
- **First-connect race.** Unity starts → spawns sidecar → sidecar binds `:8090`. There's a window (~100ms) where Tauri can connect to `:8090` before the sidecar's Unity-side IPC is alive. Mitigation: sidecar accepts TCP immediately, responds `503 Service Starting` (or queues briefly) until IPC handshake completes.
- **Performance overhead.** Each tool call now traverses two process boundaries (TCP→sidecar→IPC→Unity, and reverse). For loopback, both hops are microseconds — negligible against actual tool work in Unity (often 10ms-1s+). Verify with a perf bench in spec phase, especially for `tools/list` (157KB payload).
- **Sidecar binary distribution.** Adds one file to the package install (Node script if we go Node, compiled binary if we go Rust). Mitigation: reuse `Server~/dist/` infrastructure if Node; sidecar ships alongside `mcp-proxy.js`.
- **Re-introducing the in-process problem on the Unity-side IPC.** If the Unity-side named pipe / UDS server suffers the same `SafeHandle` race as the current TCP listener, we've moved the problem rather than solving it. Mitigation: named pipes use a different OS API (CreateNamedPipe / IO completion via different mechanism) that doesn't have the same overlapped-accept refcount issue. UDS via `NamedPipeServerStream` likely benefits from the same. Validate empirically in spec phase before committing.

## Open questions

1. **Sidecar language: Node or Rust?**
   - Node reuses `Server~/` tooling and matches `mcp-proxy.js` patterns. Smallest infra delta. Pros: existing dev setup, JSON-RPC libraries plentiful. Cons: heavier runtime, slower cold-start.
   - Rust is lighter, no runtime to ship (compiled binary), faster cold-start. Cons: new build pipeline added to the package.
   - **Recommendation:** Node. Reuses existing build / install / debug flow. Cold-start time (~100ms) is acceptable since sidecar spawns at editor startup, not per-request.

2. **IPC channel: `NamedPipeServerStream` (cross-platform .NET) vs raw Win32 named pipes via P/Invoke vs gRPC over UDS?**
   - **Recommendation:** `NamedPipeServerStream`. Cross-platform in modern .NET, well-tested, no P/Invoke surface. gRPC adds a code-gen step and a heavy dependency — overkill for our payload sizes.

3. **Who spawns the sidecar — Unity or Tauri?**
   - Unity spawn (current recommendation): aligns with "Unity hosts the MCP server" mental model. Other clients (Claude Code, future external tools) just connect to a port Unity advertises. Tauri remains a regular client.
   - Tauri spawn: decouples sidecar from Unity entirely. But then sidecar lifetime is tied to Tauri being open, which breaks `mcp-proxy.js` use cases when Tauri is closed.
   - **Recommendation:** Unity. Matches existing mental model; sidecar lifecycle = editor lifecycle.

4. **What's the sidecar's behavior when no Unity is alive?**
   - Should not happen in normal flow (Unity spawns sidecar, so sidecar can't exist without Unity having existed). If it does (race condition, panic), sidecar exits on first IPC EOF or never-connects-within-10s.
   - **Recommendation:** sidecar exits on IPC failure to reach Unity within startup grace period. No "orphan sidecar" state.

5. **Multiple Unity Editors on the same machine — port collision?**
   - Today `:8090` is hardcoded. With F37, the sidecar could pick the first available port in a range and write it to a known location (alongside the auth token) for clients to discover.
   - **Recommendation:** spec-phase decision. F25 (per-project window) is the right anchor for this; defer until that lands or coordinate with it.

6. **Should the sidecar verify it's the only one running per Unity instance?**
   - Stale sidecar from a previous Unity crash could try to bind `:8090` and conflict with the new Unity's spawn.
   - **Recommendation:** sidecar writes its PID to `Library/GameDeck/sidecar.pid`. Unity reads on spawn — if a sidecar PID is alive, kill it before starting the new one. Standard pattern.

## Related notes

- [[KI-012]] documents the root-cause investigation that led to this feature. The orphan listener is one user-visible symptom; the underlying issue is broader (any in-process network server in Unity's AppDomain suffers the same class of reload churn).
- F22 (Process Hardening) is the closest sibling — same "manage a long-running child process robustly" problem space. F37 adds one more managed process; F22's lifecycle patterns extend naturally to cover it.
- F02 (Claude Code Supervisor) established the Node child process pattern via `sdk_entry.js`. F37 reuses the same shape on the Unity side.
- Implementation surface estimate: 7–10 days including specs/tasks/testing. New code is in `Server~/src/sidecar/` (Node), `Editor/MCP/Sidecar/` (C# IPC client + spawn logic), and small touches to `Editor/MCP/Server/McpServer.cs` (replace TCP listener with IPC client). Comparable to F02 in surface area.

## References

- [Editor/MCP/Server/McpServer.cs](../../Editor/MCP/Server/McpServer.cs) — current in-process implementation to be slimmed.
- [Server~/src/mcp-proxy.ts](../../Server~/src/mcp-proxy.ts) — existing proxy pattern, similar shape to the sidecar.
- [App~/src-tauri/src/unity_client/](../../App~/src-tauri/src/unity_client/) — Tauri-side TCP client, no changes needed.
- [known-issues.md — KI-012](../known-issues.md) — historical investigation of the in-process race.
