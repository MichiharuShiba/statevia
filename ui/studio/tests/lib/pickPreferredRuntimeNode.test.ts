import { describe, expect, it } from "vitest";
import { pickPreferredRuntimeNode } from "../../features/executions/lib/pickPreferredRuntimeNode";
import type { ExecutionNodeDTO } from "@/features/executions/types";

function node(
  overrides: Partial<ExecutionNodeDTO> & Pick<ExecutionNodeDTO, "nodeId" | "status" | "attempt">
): ExecutionNodeDTO {
  return {
    nodeName: "cycle.decide",
    nodeType: "Wait",
    workerId: null,
    waitKey: null,
    allowedEvents: ["Again", "Finish"],
    canceledByExecution: false,
    ...overrides
  };
}

describe("pickPreferredRuntimeNode", () => {
  it("同名の完了 Wait と WAITING があるとき WAITING を選ぶ", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "decide-1", status: "SUCCEEDED", attempt: 1 }),
      node({ nodeId: "decide-2", status: "WAITING", attempt: 2 })
    ];

    // Act
    const preferred = pickPreferredRuntimeNode(nodes, "cycle.decide");

    // Assert
    expect(preferred?.nodeId).toBe("decide-2");
    expect(preferred?.status).toBe("WAITING");
  });

  it("WAITING が配列先頭でも優先する（find 先頭勝ちの回帰防止）", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "decide-1", status: "SUCCEEDED", attempt: 1 }),
      node({ nodeId: "decide-2", status: "SUCCEEDED", attempt: 2 }),
      node({ nodeId: "decide-3", status: "WAITING", attempt: 3 })
    ];

    // Act
    const preferred = pickPreferredRuntimeNode(nodes, "CYCLE.DECIDE");

    // Assert
    expect(preferred?.nodeId).toBe("decide-3");
  });

  it("すべて完了なら attempt 最大を選ぶ", () => {
    // Arrange
    const nodes = [
      node({ nodeId: "decide-1", status: "SUCCEEDED", attempt: 1 }),
      node({ nodeId: "decide-3", status: "SUCCEEDED", attempt: 3 }),
      node({ nodeId: "decide-2", status: "SUCCEEDED", attempt: 2 })
    ];

    // Act
    const preferred = pickPreferredRuntimeNode(nodes, "cycle.decide");

    // Assert
    expect(preferred?.nodeId).toBe("decide-3");
  });

  it("該当 nodeName が無ければ undefined", () => {
    // Arrange
    const nodes = [node({ nodeId: "decide-1", status: "WAITING", attempt: 1 })];

    // Act / Assert
    expect(pickPreferredRuntimeNode(nodes, "cycle.fork")).toBeUndefined();
  });
});
