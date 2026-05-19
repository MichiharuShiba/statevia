import { describe, expect, it } from "vitest";
import { defaultDefinitionYaml } from "../../app/lib/defaultDefinitionYaml";

describe("defaultDefinitionYaml", () => {
  it("最小ワークフローの YAML テンプレートを含む", () => {
    expect(defaultDefinitionYaml).toContain("version: 1");
    expect(defaultDefinitionYaml).toContain("type: start");
    expect(defaultDefinitionYaml).toContain("type: end");
    expect(defaultDefinitionYaml).toContain("fork1");
    expect(defaultDefinitionYaml).toContain("join1");
  });
});
