import { describe, expect, it } from "vitest";
import { buildDocumentAdjacency } from "@/features/definition-editor/lib/definitionGraphAdjacency";
import type { DefinitionGraphDocument } from "@/features/definition-editor/lib/types";

describe("buildDocumentAdjacency", () => {
  it("next / error / edges / fork branches から有向辺を構築する", () => {
    const document: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "wf" },
      nodes: [
        { name: "Start", type: "start", next: "Action" },
        { name: "Action", type: "action", action: "noop", next: "Fork", error: "End" },
        {
          name: "Fork",
          type: "fork",
          branches: ["JoinA", "JoinB"],
          edges: [{ to: "JoinA" }, { to: "JoinB", when: { path: "$.x", op: "eq", value: 1 } }]
        },
        { name: "JoinA", type: "join", next: "End" },
        { name: "JoinB", type: "join" },
        { name: "End", type: "end" }
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
