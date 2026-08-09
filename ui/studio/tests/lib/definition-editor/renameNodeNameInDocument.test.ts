import { describe, expect, it } from "vitest";
import { renameNodeNameInDocument } from "@/features/definition-editor/lib/renameNodeNameInDocument";
import type { DefinitionGraphDocument } from "@/features/definition-editor/lib/types";

function doc(overrides: Partial<DefinitionGraphDocument> = {}): DefinitionGraphDocument {
  return {
    version: 1,
    workflow: { name: "w" },
    nodes: [],
    ...overrides
  };
}

describe("renameNodeNameInDocument", () => {
  it("ノード名を変え、next / edges / branches / meta.layout を同期する", () => {
    const before = doc({
      nodes: [
        { name: "a", type: "start" },
        { name: "b", type: "action", next: "c", error: "c", action: "x" },
        { name: "c", type: "action", action: "y" },
        { name: "fork1", type: "fork", branches: ["c", "d"] },
        { name: "d", type: "end" },
        { name: "j", type: "join", edges: [{ to: "c" }, { to: "d" }] }
      ],
      meta: { layout: { c: { x: 1, y: 2 }, d: { x: 3, y: 4 } } }
    });

    const after = renameNodeNameInDocument(before, "c", "c2");

    const byName = new Map(after.nodes.map((n) => [n.name, n] as const));
    expect(byName.get("c")).toBeUndefined();
    expect(byName.get("c2")?.name).toBe("c2");
    expect(byName.get("b")?.next).toBe("c2");
    expect(byName.get("b")?.error).toBe("c2");
    expect(byName.get("fork1")?.branches).toEqual(["c2", "d"]);
    expect(byName.get("j")?.edges?.[0]?.to).toBe("c2");
    expect(after.meta?.layout?.c2).toEqual({ x: 1, y: 2 });
    expect(after.meta?.layout?.c).toBeUndefined();
  });

  it("fromName === toName のときは何も変えない", () => {
    const d = doc({ nodes: [{ name: "a", type: "start" }] });
    expect(renameNodeNameInDocument(d, "a", "a")).toBe(d);
  });
});
