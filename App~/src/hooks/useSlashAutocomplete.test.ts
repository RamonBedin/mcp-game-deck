/**
 * Unit tests for the pure helpers in `useSlashAutocomplete`. The
 * React hook itself is exercised by the chat-input wiring in task
 * 5.3; here we lock down only the parsing + filter + insertion logic
 * because those are the parts that have to behave the same on every
 * keystroke regardless of React lifecycle.
 */

import { describe, expect, it } from "vitest";
import type { CatalogCommand } from "../ipc/types";
import { applySlashSelection, computeSlashAutocompleteState, } from "./useSlashAutocomplete";

function cmd(name: string, description = "", source: CatalogCommand["source"] = "plugin",): CatalogCommand
{
  return { name, description, source };
}

describe("computeSlashAutocompleteState — trigger detection", () => {
  it("triggers at start of input", () => {
    const result = computeSlashAutocompleteState("/he", 3, [cmd("help")]);
    expect(result.active).toBe(true);
    expect(result.query).toBe("he");
    expect(result.range).toEqual([0, 3]);
  });

  it("triggers mid-message after space", () => {
    const result = computeSlashAutocompleteState("hello /cl", 9, [cmd("clear")]);
    expect(result.active).toBe(true);
    expect(result.query).toBe("cl");
    expect(result.range).toEqual([6, 9]);
  });

  it("does NOT trigger after a letter (abc/foo)", () => {
    const result = computeSlashAutocompleteState("abc/foo", 7, [cmd("foo")]);
    expect(result.active).toBe(false);
    expect(result.range).toBeNull();
  });

  it("does NOT trigger inside a URL (https://example.com/foo)", () => {
    const result = computeSlashAutocompleteState(
      "https://example.com/foo",
      23,
      [cmd("foo")],
    );
    expect(result.active).toBe(false);
    expect(result.range).toBeNull();
  });

  it("does NOT trigger when query contains whitespace", () => {
    const result = computeSlashAutocompleteState("/sa ve", 6, [cmd("save-plan")]);
    expect(result.active).toBe(false);
    expect(result.range).toBeNull();
  });

  it("does NOT trigger when there is no slash before cursor", () => {
    const result = computeSlashAutocompleteState("hello world", 11, [cmd("help")]);
    expect(result.active).toBe(false);
    expect(result.range).toBeNull();
  });
});

describe("computeSlashAutocompleteState — filter behavior", () => {
  const commands: CatalogCommand[] = [
    cmd("clear", "Clear the conversation"),
    cmd("help", "Show available commands"),
    cmd("save-plan", "Save the current plan"),
    cmd("plan-execute", "Run a saved plan"),
    cmd("mcp-game-deck:save-plan", "Save plan via plugin"),
    cmd("inspect", "Look around without changing anything"),
  ];

  it("empty query returns all commands", () => {
    const result = computeSlashAutocompleteState("/", 1, commands);
    expect(result.active).toBe(true);
    expect(result.candidates).toHaveLength(commands.length);
  });

  it("partial query filters by substring", () => {
    const result = computeSlashAutocompleteState("/cle", 4, commands);
    expect(result.candidates.map((c) => c.name)).toEqual(["clear"]);
  });

  it("exact prefix match sorts above substring match", () => {
    const result = computeSlashAutocompleteState("/plan", 5, commands);
    const names = result.candidates.map((c) => c.name);
    const prefixIdx = names.indexOf("plan-execute");
    const substringIdx = names.indexOf("save-plan");
    expect(prefixIdx).toBeGreaterThanOrEqual(0);
    expect(substringIdx).toBeGreaterThanOrEqual(0);
    expect(prefixIdx).toBeLessThan(substringIdx);
  });

  it("mcp-game-deck: prefix sorts above unprefixed within same tier", () => {
    const result = computeSlashAutocompleteState("/plan", 5, commands);
    const names = result.candidates.map((c) => c.name);
    const mcpIdx = names.indexOf("mcp-game-deck:save-plan");
    const plainIdx = names.indexOf("save-plan");
    expect(mcpIdx).toBeGreaterThanOrEqual(0);
    expect(plainIdx).toBeGreaterThanOrEqual(0);
    expect(mcpIdx).toBeLessThan(plainIdx);
  });

  it("case-insensitive: query '/CLE' matches 'clear'", () => {
    const result = computeSlashAutocompleteState("/CLE", 4, commands);
    expect(result.candidates.map((c) => c.name)).toContain("clear");
  });

  it("description match only sorts at tier 3, below substring-name matches", () => {
    const subset: CatalogCommand[] = [
      cmd("inspect", "Look around without changing anything"),
      cmd("around-here", "A different sort of command"),
    ];
    const result = computeSlashAutocompleteState("/around", 7, subset);
    const names = result.candidates.map((c) => c.name);
    expect(names).toEqual(["around-here", "inspect"]);
  });
});

describe("applySlashSelection", () => {
  it("replaces range with /<name> (trailing space)", () => {
    const result = applySlashSelection("/cle", [0, 4], "clear");
    expect(result.newValue).toBe("/clear ");
    expect(result.newCursor).toBe(7);
  });

  it("preserves text before and after the range", () => {
    const value = "hello /sa world";
    const result = applySlashSelection(value, [6, 9], "save-plan");
    expect(result.newValue).toBe("hello /save-plan  world");
    expect(result.newCursor).toBe(6 + "/save-plan ".length);
  });
});