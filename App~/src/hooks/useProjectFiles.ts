/**
 * Subscription hook for the project file index.
 *
 * Mount-time fires a one-shot `listProjectFiles()` so the cache is
 * warm before the user types `@`. The Tauri-side `project-files-changed`
 * event refetches on every notify-debouncer-mini batch — the payload's
 * `debounced` flag is informational, so this hook ignores it and just
 * pulls a fresh list on every signal.
 *
 * **Error model:** the `list_project_files` Tauri command currently
 * always returns `Vec<FileIndexEntry>` (no `Result` wrapper), so the
 * `error` field never populates from a missing project root — that
 * case surfaces as `files: []` instead. The `try/catch` around
 * `invoke` exists for the rare IPC-layer failure mode (Tauri host
 * unavailable, protocol mismatch). If a later iteration of the
 * command promotes its return type to `Result<_, AppError>`, the
 * hook's surface absorbs it without consumer churn.
 */

import { useEffect, useState } from "react";
import { listProjectFiles } from "../ipc/commands";
import { onProjectFilesChanged } from "../ipc/events";
import type { AppError, FileIndexEntry } from "../ipc/types";

// #region Types

/**
 * Return shape of {@link useProjectFiles}. `loading` flips to `false`
 * after the first fetch completes (success OR error); subsequent
 * refetches do not toggle `loading` back to `true` so the consumer
 * UI doesn't flicker on every watcher event.
 */
export interface UseProjectFilesResult
{
  files: FileIndexEntry[];
  loading: boolean;
  error?: AppError;
}

// #endregion

// #region Hook

/**
 * Subscribes to the project file index for the lifetime of the
 * calling component. See module docblock for the lifecycle and
 * error-model notes.
 *
 * @returns `{ files, loading, error? }`.
 */
export function useProjectFiles(): UseProjectFilesResult
{
  const [state, setState] = useState<UseProjectFilesResult>({
    files: [],
    loading: true,
    error: undefined,
  });

  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    const fetchFiles = async () => {
      try
      {
        const files = await listProjectFiles();

        if (cancelled)
        {
          return;
        }

        setState({ files, loading: false, error: undefined });
      }
      catch (err)
      {
        if (cancelled)
        {
          return;
        }

        console.error("[project-files] list failed:", err);
        setState((prev) => ({
          files: prev.files,
          loading: false,
          error: err as AppError,
        }));
      }
    };

    void fetchFiles();

    onProjectFilesChanged(() => {
      if (cancelled)
      {
        return;
      }

      void fetchFiles();
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlisten = u;
        }
      })
      .catch((err) => {
        console.error("[project-files] failed to subscribe to project-files-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);

  return state;
}

// #endregion