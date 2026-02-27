import { describe, expect, it } from "vitest";
import { computeExecutionDiff } from "../../app/lib/executionDiff";
import type { ExecutionDTO } from "../../app/lib/types";

function node(
  nodeId: string,
  status: ExecutionDTO["nodes"][0]["status"],
  nodeType = "Task"
): ExecutionDTO["nodes"][0] {
  return {
    nodeId,
    nodeType,
    status,
    attempt: 1,
    workerId: null,
    waitKey: null,
    canceledByExecution: false
  };
}

function exec(
  executionId: string,
  nodes: ExecutionDTO["nodes"],
  status: ExecutionDTO["status"] = "ACTIVE"
): ExecutionDTO {
  return {
    executionId,
    status,
    graphId: "g1",
    cancelRequestedAt: null,
    canceledAt: null,
    failedAt: null,
    completedAt: null,
    nodes
  };
}

describe("computeExecutionDiff", () => {
  it("両方 null のとき null を返す", () => {
    expect(computeExecutionDiff(null, null)).toBeNull();
    expect(computeExecutionDiff(exec("a", [node("n1", "SUCCEEDED")]), null)).toBeNull();
    expect(computeExecutionDiff(null, exec("b", [node("n1", "SUCCEEDED")]))).toBeNull();
  });

  it("同一ノード・同一ステータスのとき差分なし", () => {
    const a = exec("a", [node("n1", "SUCCEEDED"), node("n2", "IDLE")]);
    const b = exec("b", [node("n1", "SUCCEEDED"), node("n2", "IDLE")]);
    const result = computeExecutionDiff(a, b);
    expect(result).not.toBeNull();
    expect(result!.nodeDiffs).toHaveLength(0);
    expect(Object.keys(result!.nodeHighlights)).toHaveLength(0);
  });

  it("ステータス差分を検出し Failed/Canceled を先頭に並べる", () => {
    const a = exec("a", [
      node("n1", "SUCCEEDED"),
      node("n2", "FAILED"),
      node("n3", "RUNNING")
    ]);
    const b = exec("b", [
      node("n1", "SUCCEEDED"),
      node("n2", "SUCCEEDED"),
      node("n3", "CANCELED")
    ]);
    const result = computeExecutionDiff(a, b);
    expect(result).not.toBeNull();
    expect(result!.nodeDiffs).toHaveLength(2);
    expect(result!.nodeDiffs[0].nodeId).toBe("n2");
    expect(result!.nodeDiffs[0].isFailureOrCancel).toBe(true);
    expect(result!.nodeDiffs[1].nodeId).toBe("n3");
    expect(result!.nodeDiffs[1].isFailureOrCancel).toBe(true);
    expect(result!.nodeHighlights["n2"]).toEqual({ isFailureOrCancel: true });
    expect(result!.nodeHighlights["n3"]).toEqual({ isFailureOrCancel: true });
  });

  it("片方にのみ存在するノードを検出する（A のみ）", () => {
    const a = exec("a", [node("n1", "SUCCEEDED"), node("n2", "IDLE")]);
    const b = exec("b", [node("n1", "SUCCEEDED")]);
    const result = computeExecutionDiff(a, b);
    expect(result).not.toBeNull();
    expect(result!.onlyInLeft).toContain("n2");
    expect(result!.onlyInRight).toHaveLength(0);
    const onlyLeft = result!.nodeDiffs.find((d) => d.kind === "only_in_left");
    expect(onlyLeft?.nodeId).toBe("n2");
  });

  it("片方にのみ存在するノードを検出する（B のみ）", () => {
    const a = exec("a", [node("n1", "SUCCEEDED")]);
    const b = exec("b", [node("n1", "SUCCEEDED"), node("n2", "FAILED")]);
    const result = computeExecutionDiff(a, b);
    expect(result).not.toBeNull();
    expect(result!.onlyInLeft).toHaveLength(0);
    expect(result!.onlyInRight).toContain("n2");
    const onlyRight = result!.nodeDiffs.find((d) => d.kind === "only_in_right");
    expect(onlyRight?.nodeId).toBe("n2");
    expect(onlyRight?.statusRight).toBe("FAILED");
    expect(onlyRight?.isFailureOrCancel).toBe(true);
    expect(result!.nodeHighlights["n2"]).toEqual({ isFailureOrCancel: true });
  });

  it("ステータス差分で Failed/Canceled でないものはソートで後に並ぶ", () => {
    const a = exec("a", [
      node("n1", "SUCCEEDED"),
      node("n2", "RUNNING"),
      node("n3", "IDLE")
    ]);
    const b = exec("b", [
      node("n1", "SUCCEEDED"),
      node("n2", "SUCCEEDED"),
      node("n3", "READY")
    ]);
    const result = computeExecutionDiff(a, b);
    expect(result).not.toBeNull();
    expect(result!.nodeDiffs).toHaveLength(2);
    expect(result!.nodeDiffs.every((d) => d.kind === "status_diff")).toBe(true);
    expect(result!.nodeDiffs.every((d) => !d.isFailureOrCancel)).toBe(true);
    expect(result!.nodeHighlights["n2"]).toEqual({ isFailureOrCancel: false });
    expect(result!.nodeHighlights["n3"]).toEqual({ isFailureOrCancel: false });
  });
});
