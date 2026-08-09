import { describe, expect, it } from "vitest";
import { mapGraphDefinitionResponse } from "../../../features/executions/hooks/mapGraphDefinitionResponse";

describe("mapGraphDefinitionResponse", () => {
  it("camelCase の GraphDefinitionResponse を変換する", () => {
    const raw = {
      graphId: "hello",
      nodes: [
        { nodeName: "a", nodeType: "Start", label: "A" },
        { nodeName: "b", nodeType: "Task", label: "B" }
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

  it("node の nodeName を GraphNodeDef.nodeName に引き継ぐ", () => {
    const def = mapGraphDefinitionResponse(
      {
        graphId: "g",
        nodes: [{ nodeName: "s1", nodeType: "Task" }],
        edges: []
      },
      "g"
    );
    expect(def?.nodes[0]).toMatchObject({ nodeName: "s1" });
  });

  it("nodes が空なら null", () => {
    expect(mapGraphDefinitionResponse({ graphId: "x", nodes: [] }, "x")).toBeNull();
  });

  it("meta.layout のノード座標を GraphDefinition.meta に載せる", () => {
    const def = mapGraphDefinitionResponse(
      {
        graphId: "g",
        nodes: [{ nodeName: "a", nodeType: "Start" }],
        edges: [],
        meta: { layout: { a: { x: 10, y: 20 } } }
      },
      "g"
    );
    expect(def?.meta?.layout?.a).toEqual({ x: 10, y: 20 });
  });
});
