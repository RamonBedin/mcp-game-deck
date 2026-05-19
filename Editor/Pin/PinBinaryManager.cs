#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GameDeck.MCP.Utils;
using UnityEditor.PackageManager;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Discovers whether the Tauri app binary is installed on disk for the current
    /// package version, resolves that version from the package metadata, and downloads
    /// the binary from the convention-based GitHub Release URL with SHA-256 verification.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="PinPolling"/> to decide between
    /// <see cref="EPinStatus.NOT_RUNNING"/> (binary present, app not running) and
    /// <see cref="EPinStatus.NOT_INSTALLED"/> (binary missing) when the TCP probe
    /// fails, and by the dropdown's About entry to show the real app version. The
    /// download pipeline streams to a sibling <c>.download</c> temp file, hashes it,
    /// compares with the remote <c>.sha256</c> sidecar (case-insensitive hex), then
    /// promotes the temp file into place via delete-then-move. <c>chmod +x</c> is
    /// applied on macOS / Linux. v2.0 publishes Windows binaries only — the URL
    /// hardcodes <c>.exe</c>; macOS / Linux requests will 404 and surface as
    /// <see cref="EDownloadResult.NETWORK_ERROR"/>.
    /// </remarks>
    public static class PinBinaryManager
    {
        #region CONSTANTS

        private const string DOWNLOAD_URL_FORMAT = "https://github.com/RamonBedin/mcp-game-deck/releases/download/v{0}/mcp-game-deck-app-{0}.exe";
        private const int DOWNLOAD_TIMEOUT_SECONDS = 60;
        private const int COPY_BUFFER_SIZE = 81920;
        private const int CHMOD_TIMEOUT_MS = 5000;
        private const int SHA256_HEX_LENGTH = 64;
        private const string TEMP_DOWNLOAD_SUFFIX = ".download";
        private const string SHA256_EXTENSION = ".sha256";
        private const string USER_AGENT_PREFIX = "mcp-game-deck-pin";
        private const string USER_AGENT_DEV_VERSION = "dev";

        #endregion

        #region FIELDS

        private static readonly HttpClient _httpClient = CreateHttpClient();
        private static readonly char[] _whitespaceChars = { ' ', '\t', '\n', '\r' };

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Builds the singleton <see cref="HttpClient"/> used for all release downloads.
        /// </summary>
        /// <returns>An <see cref="HttpClient"/> with a 60s timeout and a User-Agent of
        /// the form <c>mcp-game-deck-pin/&lt;package-version&gt;</c> (or
        /// <c>mcp-game-deck-pin/dev</c> when the package version cannot be resolved).</returns>
        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(DOWNLOAD_TIMEOUT_SECONDS),
            };

            var version = GetCurrentVersion() ?? USER_AGENT_DEV_VERSION;
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{USER_AGENT_PREFIX}/{version}");
            return client;
        }

        /// <summary>
        /// Streams the response body into <paramref name="tempPath"/>, reporting
        /// fractional progress when a <c>Content-Length</c> header was supplied.
        /// </summary>
        /// <param name="response">In-flight HTTP response whose body is being read.</param>
        /// <param name="tempPath">Sibling <c>.download</c> path the bytes are written to.</param>
        /// <param name="progress">Optional progress sink reporting <c>[0, 1]</c>.</param>
        /// <param name="ct">Cancellation token honored on every read/write loop iteration.</param>
        /// <returns>A task that completes when the body has been fully written to disk.</returns>
        private static async Task StreamToFileAsync(HttpResponseMessage response, string tempPath, IProgress<float>? progress, CancellationToken ct)
        {
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[COPY_BUFFER_SIZE];
            long readSoFar = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read, ct);
                readSoFar += read;

                if (totalBytes > 0)
                {
                    progress?.Report((float)readSoFar / totalBytes);
                }
            }
        }

        /// <summary>
        /// Computes the SHA-256 of the file at <paramref name="path"/> and returns it
        /// as a 64-character lowercase hex string.
        /// </summary>
        /// <param name="path">Absolute path to the file to hash.</param>
        /// <returns>Lowercase 64-character hex representation of the SHA-256 digest.</returns>
        private static string ComputeSha256Hex(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fileStream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Extracts the leading whitespace-separated token from a
        /// <c>sha256sum</c>-style payload — typically <c>&lt;hex&gt;  &lt;filename&gt;</c>.
        /// </summary>
        /// <param name="content">Raw response body of the <c>.sha256</c> sidecar URL.</param>
        /// <returns>First token, or <see cref="string.Empty"/> when the payload is empty.</returns>
        private static string ParseSha256RemoteContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var tokens = content.Split(_whitespaceChars, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length > 0 ? tokens[0] : string.Empty;
        }

        /// <summary>
        /// Returns <c>true</c> when the input is exactly 64 characters long and
        /// contains only hex digits.
        /// </summary>
        /// <param name="hex">Candidate hex string to validate.</param>
        /// <returns><c>true</c> when the string is a valid SHA-256 hex digest.</returns>
        private static bool IsValidSha256Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != SHA256_HEX_LENGTH)
            {
                return false;
            }

            foreach (var c in hex)
            {
                if (!IsHexChar(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tells whether a character is a valid hex digit (<c>0-9</c>, <c>a-f</c>,
        /// or <c>A-F</c>).
        /// </summary>
        /// <param name="c">Character to inspect.</param>
        /// <returns><c>true</c> when <paramref name="c"/> is a hex digit.</returns>
        private static bool IsHexChar(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        /// <summary>
        /// Best-effort delete of <paramref name="path"/>. Logs and swallows IO errors
        /// so cleanup never masks the original cause of a download failure.
        /// </summary>
        /// <param name="path">Path to remove. Missing files are ignored.</param>
        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                McpLogger.Error($"Pin binary cleanup failed for {path}", e);
            }
        }

        /// <summary>
        /// Marks <paramref name="path"/> as executable on macOS / Linux via
        /// <c>chmod +x</c>. No-op on Windows. Failures here are logged but do not
        /// downgrade the download outcome — the file is already verified on disk and
        /// the launch path will surface any executable-bit issue at
        /// <c>Process.Start</c>.
        /// </summary>
        /// <param name="path">Absolute path of the binary to mark executable.</param>
        private static void MakeExecutableIfUnix(string path)
        {
            if (Application.platform != RuntimePlatform.OSXEditor && Application.platform != RuntimePlatform.LinuxEditor)
            {
                return;
            }

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };

                using var proc = System.Diagnostics.Process.Start(psi);

                if (proc == null)
                {
                    McpLogger.Error($"chmod +x failed: process did not start for {path}");
                    return;
                }

                if (!proc.WaitForExit(CHMOD_TIMEOUT_MS))
                {
                    McpLogger.Error($"chmod +x timed out after {CHMOD_TIMEOUT_MS} ms for {path}");
                    return;
                }

                if (proc.ExitCode != 0)
                {
                    McpLogger.Error($"chmod +x exited with code {proc.ExitCode} for {path}");
                }
            }
            catch (Exception e)
            {
                McpLogger.Error($"chmod +x failed for {path}", e);
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Checks whether the per-version Tauri app binary is present on disk.
        /// </summary>
        /// <param name="version">Package version string (e.g. <c>"1.1.0"</c>).
        /// Empty / whitespace versions return <c>false</c>.</param>
        /// <returns><c>true</c> if a file exists at
        /// <see cref="PinPaths.BinaryPath(string)"/> for <paramref name="version"/>;
        /// <c>false</c> otherwise.</returns>
        public static bool IsInstalled(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            return File.Exists(PinPaths.BinaryPath(version));
        }

        /// <summary>
        /// Resolves the currently-loaded package version from the assembly's
        /// <see cref="PackageInfo"/> metadata.
        /// </summary>
        /// <returns>Version string (e.g. <c>"1.1.0"</c>) or <c>null</c> when the
        /// package metadata cannot be resolved (for example when the source loads
        /// from a non-package folder during local development).</returns>
        public static string? GetCurrentVersion()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(PinToolbarElement).Assembly);
            return packageInfo?.version;
        }

        /// <summary>
        /// Builds the convention-based GitHub Release download URL for the binary
        /// asset of the given <paramref name="version"/>.
        /// </summary>
        /// <param name="version">Package version (e.g. <c>"1.1.0"</c>). Must be
        /// non-empty.</param>
        /// <returns>Absolute HTTPS URL of the form
        /// <c>https://github.com/.../v{version}/mcp-game-deck-app-{version}.exe</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="version"/>
        /// is null, empty, or whitespace.</exception>
        public static string GetDownloadUrl(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Version must be a non-empty string.", nameof(version));
            }

            return string.Format(DOWNLOAD_URL_FORMAT, version);
        }

        /// <summary>
        /// Downloads the Tauri app binary for <paramref name="version"/> from the
        /// convention-based GitHub Release URL, verifies its SHA-256 against the
        /// sibling <c>.sha256</c> file, and promotes it into
        /// <see cref="PinPaths.BinaryPath(string)"/> on success.
        /// </summary>
        /// <param name="version">Package version to download (e.g. <c>"1.1.0"</c>).
        /// Must be non-empty.</param>
        /// <param name="progress">Optional progress sink reporting fractional download
        /// progress in the <c>[0, 1]</c> range. Reports happen only when the server
        /// returned a <c>Content-Length</c> header.</param>
        /// <param name="ct">Cancellation token. Cooperative cancellation is propagated
        /// as <see cref="OperationCanceledException"/> rather than mapped to an enum
        /// value.</param>
        /// <returns><see cref="EDownloadResult.SUCCESS"/> when the binary is downloaded,
        /// verified, and installed; <see cref="EDownloadResult.HASH_MISMATCH"/> when
        /// the local SHA-256 differs from the remote sidecar (or the sidecar is
        /// malformed); <see cref="EDownloadResult.NETWORK_ERROR"/> for any other
        /// HTTP / I/O failure.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="version"/>
        /// is null, empty, or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown when
        /// <paramref name="ct"/> is canceled before the operation completes.</exception>
        public static async Task<EDownloadResult> DownloadAsync(string version, IProgress<float>? progress = null, CancellationToken ct = default)
        {
            var binaryUrl = GetDownloadUrl(version);
            var sha256Url = binaryUrl + SHA256_EXTENSION;
            var binFolder = PinPaths.BinFolder(version);
            var finalPath = PinPaths.BinaryPath(version);
            var tempPath = finalPath + TEMP_DOWNLOAD_SUFFIX;
            try
            {
                Directory.CreateDirectory(binFolder);

                using (var response = await _httpClient.GetAsync(binaryUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    await StreamToFileAsync(response, tempPath, progress, ct);
                }

                var localHash = ComputeSha256Hex(tempPath);

                string remoteHash;

                using (var shaResponse = await _httpClient.GetAsync(sha256Url, ct))
                {
                    shaResponse.EnsureSuccessStatusCode();
                    var shaContent = await shaResponse.Content.ReadAsStringAsync();
                    remoteHash = ParseSha256RemoteContent(shaContent);
                }

                if (!IsValidSha256Hex(remoteHash) || !string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
                {
                    DeleteIfExists(tempPath);
                    return EDownloadResult.HASH_MISMATCH;
                }

                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }

                File.Move(tempPath, finalPath);

                MakeExecutableIfUnix(finalPath);

                return EDownloadResult.SUCCESS;
            }
            catch (OperationCanceledException)
            {
                DeleteIfExists(tempPath);
                throw;
            }
            catch (HttpRequestException e)
            {
                DeleteIfExists(tempPath);
                McpLogger.Info($"Pin binary download failed (network): {e.Message}");
                return EDownloadResult.NETWORK_ERROR;
            }
            catch (IOException e)
            {
                DeleteIfExists(tempPath);
                McpLogger.Info($"Pin binary download failed (I/O): {e.Message}");
                return EDownloadResult.NETWORK_ERROR;
            }
            catch (Exception e)
            {
                DeleteIfExists(tempPath);
                McpLogger.Error("Pin binary download encountered an unexpected error", e);
                return EDownloadResult.NETWORK_ERROR;
            }
        }

        #endregion

    }
}