---
name: brainstorm
description: "Guided game concept ideation — generates ideas for gameplay mechanics, systems, or features using structured creative techniques."
argument-hint: "[topic or 'open']"
user-invocable: true
allowed-tools: Read, Glob, Grep, Write
---

When this skill is invoked:

1. **Clarify scope**: What are we brainstorming? (mechanic, system, feature, full concept)

2. **Gather context**: Read relevant knowledge-base docs and existing design docs.

3. **Generate ideas** using structured techniques:
   - **Verb-first**: Define core player verbs, then build mechanics around them
   - **Mashup**: Combine unexpected genres/mechanics for novelty
   - **MDA analysis**: Map Mechanics → Dynamics → Aesthetics for each idea
   - **Reference games**: What similar games do well, what they miss

4. **For each idea, evaluate**:
   - Feasibility with our Unity 6 + MCP tool stack
   - ScriptableObject data model potential
   - Performance implications
   - Prototype complexity

5. **Output 3-5 concepts** with:
   - Core loop description
   - Key mechanics list
   - Technical feasibility (using our MCP tools)
   - Recommended next step (prototype, design doc, or kill)
