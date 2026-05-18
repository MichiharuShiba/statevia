import { describe, expect, it } from "vitest";
import { serializeDefinitionYaml } from "../../../app/lib/definition-editor/serializeDefinitionYaml";
import type { DefinitionGraphDocument } from "../../../app/lib/definition-editor/types";

describe("serializeDefinitionYaml", () => {
  it("単一無条件 edge は next に正規化する", () => {
    const document: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "W", id: "w-1", description: "desc" },
      nodes: [
        { id: "s", type: "start", next: "a" },
        {
          id: "a",
          type: "action",
          action: "noop",
          error: "e",
          edges: [{ to: "e" }]
        },
        { id: "e", type: "end" }
      ]
    };

    const yaml = serializeDefinitionYaml(document);
    expect(yaml).toContain("next: e");
    expect(yaml).not.toContain("edges:");
    expect(yaml).toContain("error: e");
    expect(yaml).toContain("id: w-1");
    expect(yaml).toContain("description: desc");
  });

  it("fork / join mode all / 条件付き edge を出力する", () => {
    const document: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "W" },
      nodes: [
        { id: "f", type: "fork", branches: ["a", "b"] },
        { id: "j", type: "join", mode: "all", next: "e" },
        {
          id: "w",
          type: "wait",
          event: "evt",
          edges: [
            {
              to: "e",
              when: { path: "$.x", op: "EQ", value: 1 },
              order: 1,
              default: true
            }
          ]
        },
        { id: "e", type: "end" }
      ]
    };

    const yaml = serializeDefinitionYaml(document);
    expect(yaml).toContain("branches:");
    expect(yaml).toContain("mode: all");
    expect(yaml).toContain("when:");
    expect(yaml).toContain("default: true");
  });

  it("空の input オブジェクトは出力しない", () => {
    const document: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "W" },
      nodes: [{ id: "a", type: "action", action: "x", input: {} }]
    };

    const yaml = serializeDefinitionYaml(document);
    expect(yaml).not.toContain("input:");
  });
});
