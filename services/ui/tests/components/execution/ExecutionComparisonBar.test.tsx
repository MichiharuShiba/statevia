import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ExecutionComparisonBar } from "../../../app/components/execution/ExecutionComparisonBar";
import type { WorkflowView } from "../../../app/lib/types";
import { computeExecutionDiff } from "../../../app/lib/executionDiff";
import { uiText } from "../../../app/lib/uiText";

function exec(
  displayId: string,
  nodes: WorkflowView["nodes"],
  status: WorkflowView["status"] = "Running"
): WorkflowView {
  return {
    displayId,
    resourceId: displayId,
    status,
    startedAt: "2026-01-01T00:00:00Z",
    cancelRequested: false,
    restartLost: false,
    graphId: "g1",
    nodes
  };
}

const node = (
  nodeId: string,
  status: WorkflowView["nodes"][0]["status"]
): WorkflowView["nodes"][0] => ({
  nodeId,
  nodeType: "Task",
  status,
  attempt: 1,
  workerId: null,
  waitKey: null,
  canceledByExecution: false
});

describe("ExecutionComparisonBar", () => {
  it("A のみのとき実行 A（基準）を表示する", () => {
    const left = exec("ex-a", [node("n1", "SUCCEEDED")]);
    render(
      <ExecutionComparisonBar
        executionLeft={left}
        executionRight={null}
        executionIdRight=""
        onExecutionIdRightChange={() => {}}
        onLoadRight={() => {}}
        loadingRight={false}
        diff={null}
      />
    );
    expect(screen.getByText("ex-a")).toBeInTheDocument();
    expect(screen.getByText(`${uiText.entities.execution} B`)).toBeInTheDocument();
  });

  it("実行 A が null のとき「未読み込み」を表示する", () => {
    render(
      <ExecutionComparisonBar
        executionLeft={null}
        executionRight={null}
        executionIdRight=""
        onExecutionIdRightChange={() => {}}
        onLoadRight={() => {}}
        loadingRight={false}
        diff={null}
      />
    );
    expect(screen.getByText(uiText.executionComparison.state.notLoaded)).toBeInTheDocument();
  });

  it("diff が null のとき「A と B を読み込むと表示されます」を表示する", () => {
    render(
      <ExecutionComparisonBar
        executionLeft={null}
        executionRight={null}
        executionIdRight="ex-b"
        onExecutionIdRightChange={() => {}}
        onLoadRight={() => {}}
        loadingRight={false}
        diff={null}
      />
    );
    expect(screen.getByText(/A と B を読み込むと表示されます/)).toBeInTheDocument();
  });

  it("diff があるとき失敗/キャンセルとその他を表示する", () => {
    const left = exec("ex-a", [
      node("n1", "SUCCEEDED"),
      node("n2", "FAILED"),
      node("n3", "RUNNING")
    ]);
    const right = exec("ex-b", [
      node("n1", "SUCCEEDED"),
      node("n2", "SUCCEEDED"),
      node("n3", "IDLE")
    ]);
    const diff = computeExecutionDiff(left, right);
    render(
      <ExecutionComparisonBar
        executionLeft={left}
        executionRight={right}
        executionIdRight="ex-b"
        onExecutionIdRightChange={() => {}}
        onLoadRight={() => {}}
        loadingRight={false}
        diff={diff!}
      />
    );
    expect(screen.getByText("失敗 / キャンセル")).toBeInTheDocument();
    expect(screen.getByText("その他")).toBeInTheDocument();
    expect(screen.getByText("n2")).toBeInTheDocument();
    expect(screen.getByText("n3")).toBeInTheDocument();
  });

  it("差分行をクリックすると onSelectDiffNode が呼ばれる", () => {
    const left = exec("ex-a", [node("n1", "SUCCEEDED"), node("n2", "FAILED")]);
    const right = exec("ex-b", [node("n1", "SUCCEEDED"), node("n2", "SUCCEEDED")]);
    const diff = computeExecutionDiff(left, right);
    const onSelectDiffNode = vi.fn();
    render(
      <ExecutionComparisonBar
        executionLeft={left}
        executionRight={right}
        executionIdRight="ex-b"
        onExecutionIdRightChange={() => {}}
        onLoadRight={() => {}}
        loadingRight={false}
        diff={diff!}
        onSelectDiffNode={onSelectDiffNode}
      />
    );
    const n2Row = screen.getByText("n2").closest("button");
    expect(n2Row).toBeInTheDocument();
    fireEvent.click(n2Row!);
    expect(onSelectDiffNode).toHaveBeenCalledWith("n2");
  });

  it("差分が 0 件のとき「ノード差分なし」を表示する", () => {
    const left = exec("ex-a", [node("n1", "SUCCEEDED")]);
    const right = exec("ex-b", [node("n1", "SUCCEEDED")]);
    const diff = computeExecutionDiff(left, right);
    render(
      <ExecutionComparisonBar
        executionLeft={left}
        executionRight={right}
        executionIdRight="ex-b"
        onExecutionIdRightChange={() => {}}
        onLoadRight={() => {}}
        loadingRight={false}
        diff={diff!}
      />
    );
    expect(screen.getByText(uiText.executionComparison.summary.noDiff)).toBeInTheDocument();
  });
});
