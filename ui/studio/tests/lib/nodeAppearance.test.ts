import { describe, expect, it } from "vitest";
import { getNodeAppearance } from "../../app/lib/nodeAppearance";

describe("getNodeAppearance", () => {
  it("START / END / WAIT / FORK / JOIN を正規化して返す", () => {
    expect(getNodeAppearance("start").shapeKind).toBe("stadium");
    expect(getNodeAppearance("END").label).toBe("END");
    expect(getNodeAppearance("waiting").label).toBe("WAIT");
    expect(getNodeAppearance("fork").shapeKind).toBe("gatewayFork");
    expect(getNodeAppearance("join").shapeKind).toBe("gatewayJoin");
  });

  it("未知の nodeType は TASK 相当の roundedRect を返す", () => {
    const appearance = getNodeAppearance("custom");
    expect(appearance.label).toBe("CUSTOM");
    expect(appearance.shapeKind).toBe("roundedRect");
  });

  it("完了系ステータスは SUCCESS ラベルになる", () => {
    expect(getNodeAppearance("completed").label).toBe("SUCCESS");
    expect(getNodeAppearance("failed").label).toBe("FAILED");
    expect(getNodeAppearance("canceled").label).toBe("CANCELED");
  });
});
