import { describe, expect, it } from "vitest";
import { parseDefinitionYaml } from "../../../app/lib/definition-editor/parseDefinitionYaml";
import { serializeDefinitionYaml } from "../../../app/lib/definition-editor/serializeDefinitionYaml";

const parseOpts = {
  rootObjectRequired: () => "root",
  nodesArrayRequired: () => "nodes"
};

describe("parseDefinitionYaml / serializeDefinitionYaml（ローダー整合）", () => {
  it("action の input を文字列で保持し、往復できる", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - id: s
    type: start
    next: a
  - id: a
    type: action
    action: noop
    input: "$.input.x"
    next: e
  - id: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document).not.toBeNull();
    expect(r.document?.nodes.find((n) => n.id === "a")?.input).toBe("$.input.x");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.id === "a")?.input).toBe("$.input.x");
  });

  it("edges[].to がオブジェクトのとき id に正規化する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - id: s
    type: start
    next: a
  - id: a
    type: action
    action: noop
    edges:
      - to:
          id: e
  - id: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.id === "a")?.edges?.[0]?.to).toBe("e");
  });

  it("action.error を保持し、{id} 形式を正規化する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - id: s
    type: start
    next: a
  - id: a
    type: action
    action: noop
    next: e
    error:
      id: ng
  - id: ng
    type: end
  - id: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.id === "a")?.error).toBe("ng");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.id === "a")?.error).toBe("ng");
  });

  it("workflow.id / description を保持し、往復で欠落しない", () => {
    const yaml = `version: 1
workflow:
  id: wf-1
  name: MyName
  description: "Hello"
nodes:
  - id: s
    type: start
    next: e
  - id: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.workflow.name).toBe("MyName");
    expect(r.document?.workflow.id).toBe("wf-1");
    expect(r.document?.workflow.description).toBe("Hello");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.workflow.name).toBe("MyName");
    expect(again.document?.workflow.id).toBe("wf-1");
    expect(again.document?.workflow.description).toBe("Hello");
  });

  it("join に mode が無いときドキュメントへ注入しない", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - id: s
    type: start
    next: j
  - id: j
    type: join
    next: e
  - id: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.id === "j")?.mode).toBeUndefined();
  });

  it("join mode: all を保持する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - id: s
    type: start
    next: j
  - id: j
    type: join
    mode: all
    next: e
  - id: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.id === "j")?.mode).toBe("all");
  });
});
