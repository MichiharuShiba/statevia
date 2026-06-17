import { describe, expect, it } from "vitest";
import { buildDocumentAdjacency } from "../../app/lib/definition-editor/definitionGraphAdjacency";
import type { DefinitionGraphDocument } from "../../app/lib/definition-editor/types";

describe("buildDocumentAdjacency", () => {
  it("next / error / edges / fork branches から有向辺を構築する", () => {
    const document: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "wf" },
      nodes: [
        { id: "Start", type: "start", next: "Action" },
        { id: "Action", type: "action", action: "noop", next: "Fork", error: "End" },
        {
          id: "Fork",
          type: "fork",
          branches: ["JoinA", "JoinB"],
          edges: [{ to: "JoinA" }, { to: "JoinB", when: { path: "$.x", op: "eq", value: 1 } }]
        },
        { id: "JoinA", type: "join", next: "End" },
        { id: "JoinB", type: "join" },
        { id: "End", type: "end" }
      ]
    };

    expect(buildDocumentAdjacency(document)).toEqual([
      { sourceId: "Start", targetId: "Action" },
      { sourceId: "Action", targetId: "Fork" },
      { sourceId: "Action", targetId: "End" },
      { sourceId: "Fork", targetId: "JoinA" },
      { sourceId: "Fork", targetId: "JoinB" },
      { sourceId: "Fork", targetId: "JoinA" },
      { sourceId: "Fork", targetId: "JoinB" },
      { sourceId: "JoinA", targetId: "End" }
    ]);
  });
});
