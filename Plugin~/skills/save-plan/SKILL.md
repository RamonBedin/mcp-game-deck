---
name: save-plan
description: "Save a plan to ProjectSettings/GameDeck/plans/ — captures the most recent plan from this conversation, or a plan you provide directly."
argument-hint: "[plan-name]"
user-invocable: true
---

When this skill is invoked:

1. **Parse the plan name** from the user's message. If they wrote
   `/save-plan setup-2d-scene`, the name is `setup-2d-scene`. If no
   argument was provided, ask via `AskUserQuestion`:
   - "What should this plan be called?" (free-text)

2. **Validate the name**: lowercase ASCII letters, digits, and hyphens
   only; must start with a letter or digit; 1–64 characters. If
   invalid, explain why and re-ask via `AskUserQuestion` in the same
   turn.

3. **Ask the user where to get the plan content** via
   `AskUserQuestion`. Use this exact question and these three options:

   Question: "Where should I get the plan content for `<name>`?"

   Option A — label: "Use the plan from our conversation"
              description: "I just shared a plan above and want to save that one."

   Option B — label: "I'll paste the plan content"
              description: "Provide the plan as free text in the answer."

   Option C — label: "Cancel"
              description: "Don't save anything."

   If the user picks A: look at the most recent assistant message in
   this conversation that resembles a plan (markdown headers,
   numbered sections, structured outline, etc.) and use its full body
   verbatim. If there's no clear plan candidate in recent history,
   re-ask with only options B and C.

   If the user picks B: use whatever they provide as the "Other
   (specify)" free text as the plan body verbatim.

   If the user picks C: respond "Cancelled." and stop.

4. **Compose a one-line description** (≤80 characters) summarizing
   the plan. Goes into the YAML frontmatter so the Plans tab list
   can show it next to the name.

5. **Resolve the auto-suffixed filename** at
   `ProjectSettings/GameDeck/plans/<name>.md` (relative to cwd,
   which is `UNITY_PROJECT_PATH`). Use the `Glob` tool to check
   existence:
   - First try `<name>.md`
   - If it exists, try `<name>-2.md`, `<name>-3.md`, ..., up to
     `<name>-99.md`
   - Pick the first slot that doesn't exist. If all 99 are taken,
     tell the user to delete some plans with that prefix first and
     stop.

6. **Write the file** using the `Write` tool. The body must be:

   ```
   ---
   description: <one-line summary from step 4>
   ---

   <plan body from step 3, verbatim>
   ```

   Don't reformat the plan body — preserve whatever structure it had.

7. **Confirm to the user** with the actual filename used:
   - No collision: "Saved as `<name>.md`."
   - With suffix: "Saved as `<name>-N.md` (a plan named `<name>`
     already existed, so I bumped the suffix)."
