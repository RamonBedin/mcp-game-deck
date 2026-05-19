/**
 * localStorage-backed boolean state hook for collapsible sidebars.
 *
 * Each column passes a stable `key` like `nav-rail`, `sessions`,
 * `plans-list`, `rules-list`. The hook returns `[collapsed, toggle]`;
 * the value is read once at mount and persisted on every change.
 *
 * SSR-safe / first-render-safe: `window.localStorage` is guarded so
 * the hook never throws under Tauri's webview when storage is
 * temporarily unavailable (cleared, blocked, etc).
 */

import { useCallback, useEffect, useState } from "react";

// #region Constants

const STORAGE_PREFIX = "mcp-game-deck:collapsed:";

// #endregion

// #region Helpers

const readStored = (key: string): boolean => {
  try
  {
    const raw = window.localStorage.getItem(STORAGE_PREFIX + key);

    if (raw === null)
    {
      return false;
    }

    return raw === "1";
  }
  catch
  {
    return false;
  }
};

const writeStored = (key: string, value: boolean): void => {
  try
  {
    window.localStorage.setItem(STORAGE_PREFIX + key, value ? "1" : "0");
  }
  catch (err)
  {
    console.debug("[useCollapsedColumn] persist failed:", err);
  }
};

// #endregion

/**
 * Hook returning `[collapsed, toggle]` for one named column. Persists
 * across reloads under `mcp-game-deck:collapsed:<key>`.
 *
 * @param key - Stable column identifier (e.g. `nav-rail`).
 * @param defaultCollapsed - Initial value when no stored entry exists.
 * @returns `[collapsed, toggle]` tuple.
 */
export function useCollapsedColumn(key: string, defaultCollapsed = false,): [boolean, () => void]
{
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    const stored = readStored(key);

    if (stored === false && !readKeyExists(key))
    {
      return defaultCollapsed;
    }

    return stored;
  });

  useEffect(() => {
    writeStored(key, collapsed);
  }, [key, collapsed]);

  const toggle = useCallback(() => {
    setCollapsed((prev) => !prev);
  }, []);

  return [collapsed, toggle];
}

const readKeyExists = (key: string): boolean => {
  try
  {
    return window.localStorage.getItem(STORAGE_PREFIX + key) !== null;
  }
  catch
  {
    return false;
  }
};