---
name: plan-execute
description: "Load a saved plan from ProjectSettings/GameDeck/plans/ and execute it step-by-step."
argument-hint: "<plan-name>"
user-invocable: true
---

When this skill is invoked:

1. **Require the plan name** from the user's message. If they wrote
   `/plan-execute setup-2d-roguelike`, the name is
   `setup-2d-roguelike`. If no argument was provided, use the `Glob`
   tool to list `ProjectSettings/GameDeck/plans/*.md` and respond:
   - With saved plans: "Usage: `/plan-execute <name>`. Available
     plans: `<name1>`, `<name2>`, ..." (names without `.md`).
   - Empty plans dir: "No plans saved yet. Use `/save-plan` to
     create one."

   Then stop.

2. **Read the plan file** with the `Read` tool at
   `ProjectSettings/GameDeck/plans/<name>.md` (relative to cwd,
   which is `UNITY_PROJECT_PATH`). If the file doesn't exist, fall
   back to the same usage hint from step 1 (Glob for the available
   plans, list them, stop).

3. **Strip the YAML frontmatter** from the loaded content — the
   block bounded by the first pair of `---` delimiters at the top
   of the file. Keep everything after the closing `---` as the
   plan body.

4. **Announce execution** to the user:
   - "Executing plan: **`<name>`**"

5. **Execute the plan body step-by-step.** Treat the body as the
   user's working brief and follow its structure as written:
   - If the plan has numbered steps, execute them in order.
   - If the plan has ambiguities or open questions, use
     `AskUserQuestion` to clarify before acting on assumptions.
   - Use whatever tools each step requires (Edit, Bash, MCP tools,
     etc.) — those come from the parent agent's permissions, not
     from this skill's restrictions.