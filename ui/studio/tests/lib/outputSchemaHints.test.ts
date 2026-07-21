import { describe, expect, it } from "vitest";
import {
  buildOutputSchemaPathHints,
  collectUpstreamOutputPathHints,
  formatStateOutputPath
} from "../../app/lib/actionSchema/outputSchemaHints";

describe("outputSchemaHints", () => {
  it("outputSchema が未定義のときは空配列を返す", () => {
    expect(buildOutputSchemaPathHints(undefined, "Rest")).toEqual([]);
    expect(buildOutputSchemaPathHints(null, "Rest")).toEqual([]);
  });

  it("識別子 State 名では $.states.<Name>.output.<prop> を生成する", () => {
    const hints = buildOutputSchemaPathHints(
      {
        type: "object",
        properties: {
          statusCode: { type: "integer" },
          body: {}
        }
      },
      "Rest"
    );
    expect(hints).toEqual(["$.states.Rest.output.body", "$.states.Rest.output.statusCode"]);
  });

  it("ドット付き State 名ではブラケット記法を使う", () => {
    expect(formatStateOutputPath("order.notify.customer", "sent")).toBe(
      "$.states['order.notify.customer'].output.sent"
    );
    const hints = buildOutputSchemaPathHints(
      {
        type: "object",
        properties: { sent: { type: "boolean" } }
      },
      "order.notify.customer"
    );
    expect(hints).toEqual(["$.states['order.notify.customer'].output.sent"]);
  });

  it("直前 action ノードの outputSchema から Context パス候補を収集する", () => {
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
    expect(hints).toEqual(["$.states.Rest.output.body", "$.states.Rest.output.statusCode"]);
  });
});
