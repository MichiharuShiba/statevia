import { describe, expect, it } from "vitest";
import { parseDefinitionYaml } from "@/features/definition-editor/lib/parseDefinitionYaml";
import { serializeDefinitionYaml } from "@/features/definition-editor/lib/serializeDefinitionYaml";

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
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: noop
    input: "$.input.x"
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document).not.toBeNull();
    expect(r.document?.nodes.find((n) => n.name === "a")?.input).toBe("$.input.x");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.name === "a")?.input).toBe("$.input.x");
  });

  it("edges[].to がオブジェクトのとき name に正規化する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: noop
    edges:
      - to:
          name: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "a")?.edges?.[0]?.to).toBe("e");
  });

  it("action.error を保持し、{name} 形式を正規化する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: a
  - name: a
    type: action
    action: noop
    next: e
    error:
      name: ng
  - name: ng
    type: end
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "a")?.error).toBe("ng");

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.name === "a")?.error).toBe("ng");
  });

  it("workflow.id / description を保持し、往復で欠落しない", () => {
    const yaml = `version: 1
workflow:
  id: wf-1
  name: MyName
  description: "Hello"
nodes:
  - name: s
    type: start
    next: e
  - name: e
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
  - name: s
    type: start
    next: j
  - name: j
    type: join
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "j")?.mode).toBeUndefined();
  });

  it("join mode: all を保持する", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: j
  - name: j
    type: join
    mode: all
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "j")?.mode).toBe("all");
  });

  it("wait.events を保持し往復できる", () => {
    const yaml = `version: 1
workflow:
  name: W
nodes:
  - name: s
    type: start
    next: w1
  - name: w1
    type: wait
    events:
      approve: ok
      reject: ng
  - name: ok
    type: action
    action: noop
    next: e
  - name: ng
    type: action
    action: noop
    next: e
  - name: e
    type: end
`;
    const r = parseDefinitionYaml(yaml, parseOpts);
    expect(r.document?.nodes.find((n) => n.name === "w1")?.events).toEqual({
      approve: "ok",
      reject: "ng"
    });

    if (!r.document) {
      throw new Error("document should not be null");
    }
    const round = serializeDefinitionYaml(r.document);
    const again = parseDefinitionYaml(round, parseOpts);
    expect(again.document?.nodes.find((n) => n.name === "w1")?.events).toEqual({
      approve: "ok",
      reject: "ng"
    });
  });
});
