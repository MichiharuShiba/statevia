import { describe, expect, it } from "vitest";
import { getStatusStyle, getNodeSortWeight } from "../../app/lib/statusStyle";

describe("getStatusStyle", () => {
  it("Workflow status Running のスタイルを返す", () => {
    // Arrange (v2)
    const status = "Running";

    // Act
    const result = getStatusStyle(status);

    // Assert
    expect(result.badgeClass).toContain("md-sys-color-info");
    expect(result.emphasisRank).toBe(20);
  });

  it("Workflow status Completed のスタイルを返す", () => {
    // Arrange (v2)
    const status = "Completed";

    // Act
    const result = getStatusStyle(status);

    // Assert
    expect(result.badgeClass).toContain("md-sys-color-success");
    expect(result.icon).toBe("✓");
  });

  it("Node status WAITING のスタイルを返す", () => {
    // Arrange
    const status = "WAITING";

    // Act
    const result = getStatusStyle(status);

    // Assert
    expect(result.badgeClass).toContain("md-sys-color-warning");
    expect(result.icon).toBe("⏸");
  });

  it("Node status FAILED のスタイルを返す", () => {
    // Arrange (NodeStatus)
    const status = "FAILED";

    // Act
    const result = getStatusStyle(status);

    // Assert
    expect(result.badgeClass).toContain("md-sys-color-error");
    expect(result.emphasisRank).toBe(80);
  });

  it("Node status CANCELED のスタイルを返す", () => {
    // Arrange (NodeStatus)
    const status = "CANCELED";

    // Act
    const result = getStatusStyle(status);

    // Assert
    expect(result.icon).toBe("✕");
    expect(result.emphasisRank).toBe(100);
  });
});

describe("getNodeSortWeight", () => {
  it("WAITING の重みは RUNNING より小さい", () => {
    // Arrange & Act
    const waiting = getNodeSortWeight("WAITING");
    const running = getNodeSortWeight("RUNNING");

    // Assert
    expect(waiting).toBeLessThan(running);
  });

  it("WAITING の重みは 1", () => {
    // Arrange
    const status = "WAITING";

    // Act
    const result = getNodeSortWeight(status);

    // Assert
    expect(result).toBe(1);
  });

  it("IDLE の重みは 7", () => {
    // Arrange
    const status = "IDLE";

    // Act
    const result = getNodeSortWeight(status);

    // Assert
    expect(result).toBe(7);
  });

  it("全 NodeStatus で重みが異なる", () => {
    // Arrange
    const statuses = ["WAITING", "CANCELED", "FAILED", "RUNNING", "READY", "SUCCEEDED", "IDLE"] as const;

    // Act
    const weights = statuses.map((s) => getNodeSortWeight(s));

    // Assert
    const unique = new Set(weights);
    expect(unique.size).toBe(statuses.length);
  });
});

describe("getStatusStyle / getNodeSortWeight (境界値)", () => {
  it("getStatusStyle は全 WorkflowStatus でスタイルを返す", () => {
    // Arrange (v2: C# API の status 値)
    const statuses = ["Running", "Completed", "Failed", "Cancelled"] as const;

    // Act & Assert
    statuses.forEach((status) => {
      const result = getStatusStyle(status);
      expect(result).toBeDefined();
      expect(result.badgeClass).toBeTruthy();
      expect(result.icon).toBeTruthy();
    });
  });

  it("getStatusStyle は全 NodeStatus でスタイルを返す", () => {
    // Arrange
    const statuses = ["IDLE", "READY", "RUNNING", "WAITING", "SUCCEEDED", "FAILED", "CANCELED"] as const;

    // Act & Assert
    statuses.forEach((status) => {
      const result = getStatusStyle(status);
      expect(result).toBeDefined();
      expect(result.emphasisRank).toBeGreaterThanOrEqual(0);
    });
  });

  it("getNodeSortWeight の最小値は WAITING の 1", () => {
    // Arrange
    const status = "WAITING";

    // Act
    const result = getNodeSortWeight(status);

    // Assert
    expect(result).toBe(1);
  });

  it("getNodeSortWeight の最大値は IDLE の 7", () => {
    // Arrange
    const status = "IDLE";

    // Act
    const result = getNodeSortWeight(status);

    // Assert
    expect(result).toBe(7);
  });

  it("getNodeSortWeight は CANCELED で 2", () => {
    // Arrange
    const status = "CANCELED";

    // Act
    const result = getNodeSortWeight(status);

    // Assert
    expect(result).toBe(2);
  });
});
