/**
 * Subscribes to Tauri's webview-level drag-drop event and surfaces
 * the drop affordance plus the dropped file paths to the caller.
 *
 * Tauri 2 hands paths directly via `getCurrentWebview()
 * .onDragDropEvent` — no FileReader / base64 work in JS land. The
 * paths flow straight to `sendMessage` and reach the SDK boundary in
 * `sdk-entry.js`, which is where encoding actually happens.
 */

import { useEffect, useState } from "react";
import { getCurrentWebview } from "@tauri-apps/api/webview";

// #region Types

/**
 * Return shape of the `useFileDragDrop` hook.
 *
 * Exposes the live drag-over state so the consuming component can render a
 * drop-zone overlay or styling cue while the user is dragging a file across
 * the window.
 */
interface UseFileDragDropResult
{
  isDragging: boolean;
}

// #endregion

/**
 * Wires the global drag-drop listener and forwards every dropped
 * file path batch to `onDrop`. The listener is attached on mount and
 * removed on unmount — duplicate subscriptions are not safe.
 *
 * @param onDrop - Invoked once per drop event with the absolute paths.
 * @returns `{ isDragging }` so the caller can render hover state.
 */
export function useFileDragDrop(onDrop: (paths: string[]) => void,): UseFileDragDropResult
{
  const [isDragging, setIsDragging] = useState(false);

  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    getCurrentWebview()
      .onDragDropEvent((event) => {
        if (cancelled)
        {
          return;
        }

        switch (event.payload.type)
        {
          case "enter":
            setIsDragging(true);
            break;
          case "leave":
            setIsDragging(false);
            break;
          case "drop":
            setIsDragging(false);
            if (event.payload.paths.length > 0)
            {
              onDrop(event.payload.paths);
            }
            break;
          default:
            break;
        }
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
        console.error("[drag-drop] subscription failed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [onDrop]);

  return { isDragging };
}