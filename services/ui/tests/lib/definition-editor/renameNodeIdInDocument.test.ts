import { describe, expect, it } from "vitest";
import { renameNodeIdInDocument } from "../../../app/lib/definition-editor/renameNodeIdInDocument";
import type { DefinitionGraphDocument } from "../../../app/lib/definition-editor/types";

function doc(overrides: Partial<DefinitionGraphDocument> = {}): DefinitionGraphDocument {
  return {
    version: 1,
    workflow: { name: "w" },
    nodes: [],
    ...overrides
  };
}

describe("renameNodeIdInDocument", () => {
  it("ノード id を変え、next / edges / branches / meta.layout を同期する", () => {
    const before = doc({
      nodes: [
        { id: "a", type: "start" },
        { id: "b", type: "action", next: "c", action: "x" },
        { id: "c", type: "action", action: "y" },
        { id: "fork1", type: "fork", branches: ["c", "d"] },
        { id: "d", type: "end" },
        { id: "j", type: "join", edges: [{ to: "c" }, { to: "d" }] }
      ],
      meta: { layout: { c: { x: 1, y: 2 }, d: { x: 3, y: 4 } } }
    });

    const after = renameNodeIdInDocument(before, "c", "c2");

    const byId = new Map(after.nodes.map((n) => [n.id, n] as const));
    expect(byId.get("c")).toBeUndefined();
    expect(byId.get("c2")?.id).toBe("c2");
    expect(byId.get("b")?.next).toBe("c2");
    expect(byId.get("fork1")?.branches).toEqual(["c2", "d"]);
    expect(byId.get("j")?.edges?.[0]?.to).toBe("c2");
    expect(after.meta?.layout?.c2).toEqual({ x: 1, y: 2 });
    expect(after.meta?.layout?.c).toBeUndefined();
  });

  it("fromId === toId のときは何も変えない", () => {
    const d = doc({ nodes: [{ id: "a", type: "start" }] });
    expect(renameNodeIdInDocument(d, "a", "a")).toBe(d);
  });
});
