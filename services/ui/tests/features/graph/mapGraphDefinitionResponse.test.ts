import { describe, expect, it } from "vitest";
import { mapGraphDefinitionResponse } from "../../../app/features/graph/mapGraphDefinitionResponse";

describe("mapGraphDefinitionResponse", () => {
  it("camelCase の GraphDefinitionResponse を変換する", () => {
    const raw = {
      graphId: "hello",
      nodes: [
        { nodeId: "a", nodeType: "Start", label: "A" },
        { nodeId: "b", nodeType: "Task", label: "B" }
      ],
      edges: [{ from: "a", to: "b" }]
    };
    const def = mapGraphDefinitionResponse(raw, "hello");
    expect(def).not.toBeNull();
    if (def === null) throw new Error("expected non-null");
    expect(def.graphId).toBe("hello");
    expect(def.nodes).toHaveLength(2);
    expect(def.edges).toHaveLength(1);
    expect(def.edges[0].from).toBe("a");
    expect(def.edges[0].to).toBe("b");
  });

  it("nodes が空なら null", () => {
    expect(mapGraphDefinitionResponse({ graphId: "x", nodes: [] }, "x")).toBeNull();
  });
});
