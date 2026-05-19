---
name: create-command
description: "Create a new custom slash command — generates a SKILL.md template and saves it to ProjectSettings/GameDeck/commands/."
argument-hint: "[command-name]"
user-invocable: true
allowed-tools: Read, Write, Glob
---

When this skill is invoked:

1. **Parse the command name** from the user's message. If the user wrote
   `/create-command fix-imports`, the name is `fix-imports`. If no name
   was provided, ask for one. The name must be lowercase, kebab-case,
   no spaces (e.g., `my-command`, `check-layers`, `setup-scene`).

2. **Ask the user** what this command should do:
   - "What should `/<name>` do? Describe it in one sentence."
   - "What specific steps should it follow? What should it check? What
     output format should it produce?"

3. **Generate the SKILL.md** file following this exact format:

   ```
   ---
   name: <command-name>
   description: "<one-sentence description>"
   argument-hint: "<what arguments it expects, if any>"
   user-invocable: true
   allowed-tools: Read, Glob, Grep, Write, Edit, Bash
   ---

   When this skill is invoked:

   1. <step 1>
   2. <step 2>
   ...

   Output format:
   <describe the expected output>
   ```

   Base the allowed-tools on what the command needs. Read-only commands
   don't need Write/Edit. Use the existing skills in the package as
   reference for style and structure.

4. **Save the file** using the Write tool to:
   `ProjectSettings/GameDeck/commands/<name>/SKILL.md`

   Create the directory if it doesn't exist.

5. **Confirm** to the user:
   "Command `/<name>` created! You can use it now — type `/` in the
   chat and it will appear in the autocomplete."