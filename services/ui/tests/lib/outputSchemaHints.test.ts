import { describe, expect, it } from "vitest";
import { buildOutputSchemaPathHints, collectUpstreamOutputPathHints } from "../../app/lib/actionSchema/outputSchemaHints";

describe("outputSchemaHints", () => {
  it("outputSchema が未定義のときは空配列を返す", () => {
    expect(buildOutputSchemaPathHints(undefined)).toEqual([]);
    expect(buildOutputSchemaPathHints(null)).toEqual([]);
  });

  it("outputSchema.properties から when.path 候補を生成する", () => {
    const hints = buildOutputSchemaPathHints({
      type: "object",
      properties: {
        statusCode: { type: "integer" },
        body: {}
      }
    });
    expect(hints).toEqual(["$.body", "$.statusCode"]);
  });

  it("直前 action ノードの outputSchema から候補を収集する", () => {
    const nodes = [
      { id: "Rest", type: "action", action: "statevia.action.builtin.rest" },
      { id: "Next", type: "action", action: "statevia.action.builtin.noop" }
    ];
    const edges = [{ sourceId: "Rest", targetId: "Next" }];
    const outputSchemaByActionId = new Map([
      [
        "statevia.action.builtin.rest",
        {
          type: "object",
          properties: {
            statusCode: { type: "integer" },
            body: {}
          }
        }
      ]
    ]);

    const hints = collectUpstreamOutputPathHints(nodes, edges, "Next", outputSchemaByActionId);
    expect(hints).toEqual(["$.body", "$.statusCode"]);
  });
});
