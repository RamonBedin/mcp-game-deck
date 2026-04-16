---
name: refactor
description: "Analyze and refactor a file or system with before/after comparison."
argument-hint: "[file-or-system-path]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Edit, Write
---

When this skill is invoked:

1. **Parse the target path** from the user's message. If the user wrote
   `/refactor Assets/Scripts/Combat/DamageSystem.cs`, the target is that
   file. If a directory was given, treat all scripts in it as the target.
   If no path was provided, ask for one.

2. **Ask for the following inputs** before doing any analysis:

   - **Problem**: "What's wrong with the current code? What problem are
     you trying to solve?" (e.g., too many responsibilities, hard to test,
     performance issues, code duplication)
   - **Constraints**: "What must NOT change? List any API contracts,
     public interfaces, or behaviors that must remain compatible."
     (e.g., "other systems call DamageSystem.Apply(), that signature
     must stay the same")

   Wait for the user to answer both before proceeding.

3. **Read the target file(s)** in full using the Read tool. Also read:
   - Files that reference the target (use Grep to find usages)
   - Related KB docs for recommended patterns:
     - `{{KB_PATH}}/01-unity-project-architecture.md` for structure
     - `{{KB_PATH}}/03-unity-design-patterns.md` for patterns
     - Other KB docs as relevant to the specific problem

4. **Analyze the current architecture** and identify:
   - Single Responsibility violations
   - Dependency direction issues
   - Unity anti-patterns (Find, GetComponent in Update, public fields, etc.)
   - Code duplication
   - Methods exceeding 40 lines
   - Missing event-driven communication
   - Testability concerns

5. **Propose the refactored version** with a clear explanation:

   ```
   ## Refactor Plan: [File/System Name]

   ### Problem
   [Summarize what's wrong]

   ### Constraints
   [List what must not change]

   ### Changes
   [For each change]:
   - **What**: [description of the change]
   - **Why**: [which principle or pattern this fixes]
   - **Before**: [relevant code snippet]
   - **After**: [proposed code snippet]

   ### Impact
   - Files modified: [list]
   - Files created: [list, if extracting new classes]
   - Public API changes: [none / list breaking changes]

   ### Risk
   [What could break, what to test after]
   ```

6. **Wait for user confirmation** before applying any changes. Ask:
   "Apply these changes? I'll modify the files and you can review the
   diff. Say 'apply' to proceed or tell me what to adjust."

7. **Apply changes** only after the user confirms. Use Edit for surgical
   changes, Write only for new files. After applying:
   - List all modified files
   - Suggest running tests or compilation to verify
   - Note any manual steps needed (e.g., updating serialized references
     in the Inspector)