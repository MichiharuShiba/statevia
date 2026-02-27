import { describe, expect, it } from "vitest";
import { getGraphDefinition } from "../../app/graphs/registry";

describe("getGraphDefinition", () => {
  it("登録済み graphId hello の定義を返す", () => {
    // Arrange
    const graphId = "hello";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.graphId).toBe("hello");
    expect(result?.nodes).toBeDefined();
    expect(Array.isArray(result?.nodes)).toBe(true);
    expect(result?.edges).toBeDefined();
  });

  it("登録済み graphId graph-1 の定義を返す", () => {
    // Arrange
    const graphId = "graph-1";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).not.toBeNull();
    expect(result?.graphId).toBe("graph-1");
    expect(result?.nodes).toBeDefined();
    expect(result?.nodes?.length).toBeGreaterThan(0);
    expect(result?.edges).toBeDefined();
  });

  it("未登録 graphId のとき null を返す", () => {
    // Arrange
    const graphId = "non-existent-graph";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).toBeNull();
  });

  it("空文字のとき null を返す", () => {
    // Arrange
    const graphId = "";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).toBeNull();
  });
});

describe("getGraphDefinition (境界値)", () => {
  it("空白のみの graphId は未登録なので null", () => {
    // Arrange
    const graphId = "   ";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).toBeNull();
  });

  it("大文字 HELLO は登録キー hello と異なるので null", () => {
    // Arrange
    const graphId = "HELLO";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).toBeNull();
  });

  it("先頭に空白がある graphId は登録されていないので null", () => {
    // Arrange
    const graphId = " hello";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).toBeNull();
  });

  it("末尾に空白がある graphId は登録されていないので null", () => {
    // Arrange
    const graphId = "hello ";

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).toBeNull();
  });

  it("長い未登録 graphId でも null", () => {
    // Arrange
    const graphId = "a".repeat(200);

    // Act
    const result = getGraphDefinition(graphId);

    // Assert
    expect(result).toBeNull();
  });
});
