/**
 * Unit tests for the pure helpers in `useAtAutocomplete`. Mirrors
 * the test surface of `useSlashAutocomplete.test.ts`: trigger
 * detection, filter behavior, and `applyAtSelection` insertion
 * shapes. The React hook itself is exercised by the chat-input
 */

import { describe, expect, it } from "vitest";
import type { CatalogAgent, FileIndexEntry } from "../ipc/types";
import { applyAtSelection, computeAtAutocompleteState, type AtCandidate, } from "./useAtAutocomplete";

function agent(name: string, description = "", source: CatalogAgent["source"] = "plugin",): CatalogAgent
{
  return { name, description, source };
}

function file(path: string, kind: FileIndexEntry["kind"] = "file",): FileIndexEntry
{
  return { path, kind };
}

describe("computeAtAutocompleteState — trigger detection", () => {
  it("triggers at start of input", () => {
    const result = computeAtAutocompleteState("@un", 3, [agent("unity")], []);
    expect(result.active).toBe(true);
    expect(result.query).toBe("un");
    expect(result.range).toEqual([0, 3]);
  });

  it("triggers mid-message after space", () => {
    const result = computeAtAutocompleteState("hello @as", 9, [], [
      file("Assets/foo.cs"),
    ]);
    expect(result.active).toBe(true);
    expect(result.query).toBe("as");
    expect(result.range).toEqual([6, 9]);
  });

  it("does NOT trigger after a letter (email-like foo@bar.com)", () => {
    const result = computeAtAutocompleteState("foo@bar.com", 11, [agent("bar")], []);
    expect(result.active).toBe(false);
    expect(result.range).toBeNull();
  });

  it("does NOT trigger when query contains whitespace", () => {
    const result = computeAtAutocompleteState("@un ity", 7, [agent("unity")], []);
    expect(result.active).toBe(false);
    expect(result.range).toBeNull();
  });

  it("does NOT trigger when there is no @ before cursor", () => {
    const result = computeAtAutocompleteState("hello world", 11, [agent("unity")], []);
    expect(result.active).toBe(false);
    expect(result.range).toBeNull();
  });
});

describe("computeAtAutocompleteState — filter behavior", () => {
  const agents: CatalogAgent[] = [
    agent("unity-specialist", "Unity Editor expert"),
    agent("react-specialist", "React UI expert"),
    agent("docs-writer", "Documentation"),
  ];
  const files: FileIndexEntry[] = [
    file("Assets/Scripts/Player.cs"),
    file("Assets/Scripts/Enemy.cs"),
    file("Assets/Prefabs/UnityLogo.png"),
    file("README.md"),
  ];

  it("empty query returns all candidates with agents block before files block", () => {
    const result = computeAtAutocompleteState("@", 1, agents, files);
    expect(result.candidates).toHaveLength(agents.length + files.length);
    const firstFileIdx = result.candidates.findIndex((c) => c.kind === "file");
    const lastAgentIdx = result.candidates
      .map((c, i) => ({ c, i }))
      .filter(({ c }) => c.kind === "agent")
      .pop()!.i;
    expect(lastAgentIdx).toBeLessThan(firstFileIdx);
  });

  it("substring match cross-section: unity matches both", () => {
    const result = computeAtAutocompleteState("@unity", 6, agents, files);
    const names = result.candidates.map((c) =>
      c.kind === "agent" ? c.agent.name : c.file.path,
    );
    expect(names).toContain("unity-specialist");
    expect(names).toContain("Assets/Prefabs/UnityLogo.png");
  });

  it("filter matches agent description", () => {
    const result = computeAtAutocompleteState("@editor", 7, agents, files);
    const agentMatches = result.candidates
      .filter((c): c is Extract<AtCandidate, { kind: "agent" }> => c.kind === "agent")
      .map((c) => c.agent.name);
    expect(agentMatches).toContain("unity-specialist");
  });

  it("agents block always precedes files block in the concatenated list", () => {
    const result = computeAtAutocompleteState("@s", 2, agents, files);
    const kinds = result.candidates.map((c) => c.kind);
    const firstFile = kinds.indexOf("file");
    const lastAgent = kinds.lastIndexOf("agent");
    if (firstFile >= 0 && lastAgent >= 0)
    {
      expect(lastAgent).toBeLessThan(firstFile);
    }
  });

  it("case-insensitive: query '@UNITY' matches lowercase 'unity-specialist'", () => {
    const result = computeAtAutocompleteState("@UNITY", 6, agents, files);
    const names = result.candidates.map((c) =>
      c.kind === "agent" ? c.agent.name : c.file.path,
    );
    expect(names).toContain("unity-specialist");
  });

  it("agents sorted alphabetically within section", () => {
    const result = computeAtAutocompleteState("@", 1, agents, []);
    const agentNames = result.candidates
      .filter((c): c is Extract<AtCandidate, { kind: "agent" }> => c.kind === "agent")
      .map((c) => c.agent.name);
    const sorted = [...agentNames].sort((a, b) => a.localeCompare(b));
    expect(agentNames).toEqual(sorted);
  });

  it("files sorted alphabetically within section", () => {
    const result = computeAtAutocompleteState("@", 1, [], files);
    const filePaths = result.candidates
      .filter((c): c is Extract<AtCandidate, { kind: "file" }> => c.kind === "file")
      .map((c) => c.file.path);
    const sorted = [...filePaths].sort((a, b) => a.localeCompare(b));
    expect(filePaths).toEqual(sorted);
  });
});

describe("applyAtSelection", () => {
  it("agent: inserts @agent-<name> with trailing space", () => {
    const candidate: AtCandidate = {
      kind: "agent",
      agent: agent("unity-specialist"),
    };
    const result = applyAtSelection("@un", [0, 3], candidate);
    expect(result.newValue).toBe("@agent-unity-specialist ");
    expect(result.newCursor).toBe("@agent-unity-specialist ".length);
  });

  it("file: inserts @<path> with trailing space", () => {
    const candidate: AtCandidate = {
      kind: "file",
      file: file("Assets/Scripts/Foo.cs"),
    };
    const result = applyAtSelection("@as", [0, 3], candidate);
    expect(result.newValue).toBe("@Assets/Scripts/Foo.cs ");
    expect(result.newCursor).toBe("@Assets/Scripts/Foo.cs ".length);
  });

  it("preserves text before and after the range", () => {
    const candidate: AtCandidate = {
      kind: "file",
      file: file("README.md"),
    };
    const value = "see @RE for details";
    const result = applyAtSelection(value, [4, 7], candidate);
    expect(result.newValue).toBe("see @README.md  for details");
    expect(result.newCursor).toBe(4 + "@README.md ".length);
  });
});