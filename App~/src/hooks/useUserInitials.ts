/**
 * Returns the 2-letter avatar initials for the current OS user.
 *
 * Resolves once per app lifetime: the first caller triggers a Tauri
 * `get_os_username` invocation, derives initials via `deriveInitials`,
 * and caches the result in module scope. Subsequent renders (and
 * subsequent components) reuse the cache — there is no second roundtrip
 * to Rust. Returns `"??"` while loading and on failure so the avatar
 * always renders a stable 2-char box.
 */

import { useEffect, useState } from "react";
import { getOsUsername } from "../ipc/commands";
import { deriveInitials } from "../utils/initials";

let cached: string | null = null;
let pending: Promise<string> | null = null;

/**
 * Read the cached OS-user initials, kicking off the one-shot fetch on
 * first call. Always returns a 2-character string.
 *
 * @returns The user's initials, or `"??"` while the fetch is in flight.
 */
export function useUserInitials(): string
{
  const [initials, setInitials] = useState<string>(cached ?? "??");

  useEffect(() => {
    if (cached !== null)
    {
      return;
    }

    if (pending === null)
    {
      pending = getOsUsername()
        .then((name) => deriveInitials(name))
        .catch((err) => {
          console.error("[user-initials] get_os_username failed:", err);
          return "??";
        });
    }

    void pending.then((value) => {
      cached = value;
      setInitials(value);
    });
  }, []);

  return initials;
}