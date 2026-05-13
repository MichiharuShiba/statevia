import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { NodeDetail } from "../../../app/components/nodes/NodeDetail";
import { uiText } from "../../../app/lib/uiText";
import type { ExecutionNodeDTO, WorkflowView } from "../../../app/lib/types";

const baseExecution: WorkflowView = {
  displayId: "ex-1",
  resourceId: "res-1",
  status: "Running",
  startedAt: "2026-01-01T00:00:00Z",
  cancelRequested: false,
  restartLost: false,
  graphId: "g-1",
  nodes: []
};

const baseNode: ExecutionNodeDTO = {
  executionNodeId: "n-1",
  nodeType: "Task",
  status: "RUNNING",
  attempt: 1,
  workerId: "w-1",
  waitKey: null,
  canceledByExecution: false
};

const defaultProps = {
  execution: baseExecution,
  node: baseNode,
  loading: false,
  onResume: vi.fn(),
  resumeDisabledReason: null as string | null
};

describe("NodeDetail", () => {
  it("execution が null のとき実行の読み込み案内を表示する", () => {
    render(
      <NodeDetail
        {...defaultProps}
        execution={null}
        node={baseNode}
      />
    );
    expect(screen.getByText(uiText.nodeDetail.prompts.loadExecution(uiText.entities.execution))).toBeInTheDocument();
  });

  it("node が null のときノード選択案内を表示する", () => {
    render(
      <NodeDetail
        {...defaultProps}
        execution={baseExecution}
        node={null}
      />
    );
    expect(screen.getByText(uiText.nodeDetail.prompts.selectNode(uiText.entities.node))).toBeInTheDocument();
  });

  it("node が選択されているとき詳細見出しと実行ノード ID を表示する", () => {
    render(<NodeDetail {...defaultProps} />);
    expect(screen.getByText(uiText.nodeDetail.title(uiText.entities.node))).toBeInTheDocument();
    expect(screen.getByText(uiText.nodeDetail.meta.executionNodeId("n-1"))).toBeInTheDocument();
    expect(screen.getByText("RUNNING")).toBeInTheDocument();
  });

  it("startedAt・completedAt があるときトレース欄に開始・終了・実行時間を表示する", () => {
    const node: ExecutionNodeDTO = {
      ...baseNode,
      status: "SUCCEEDED",
      startedAt: "2026-01-15T10:00:00.123Z",
      completedAt: "2026-01-15T10:00:01.456Z"
    };
    render(<NodeDetail {...defaultProps} node={node} />);
    expect(screen.getByText(/開始:/)).toBeInTheDocument();
    expect(screen.getByText(/終了:/)).toBeInTheDocument();
    expect(screen.getByText(/実行時間:/)).toBeInTheDocument();
    expect(screen.getByText(/123/)).toBeInTheDocument();
    expect(screen.getByText(/456/)).toBeInTheDocument();
  });

  it("output を渡すと出力見出しと内容を表示する", () => {
    const node: ExecutionNodeDTO = { ...baseNode, output: { x: 1 } };
    render(<NodeDetail {...defaultProps} node={node} />);
    fireEvent.click(screen.getByText(uiText.nodeDetail.trace.outputHeading));
    expect(screen.getByText(uiText.nodeDetail.trace.outputHeading)).toBeInTheDocument();
    expect(screen.getByText(/"x": 1/, { exact: false })).toBeInTheDocument();
  });

  it("stateName があるときメタ情報にノード名を表示する", () => {
    const node: ExecutionNodeDTO = { ...baseNode, stateName: "MyState" };
    render(<NodeDetail {...defaultProps} node={node} />);
    expect(screen.getByText(uiText.nodeDetail.meta.stateName("MyState"))).toBeInTheDocument();
  });

  it("input を渡すとトレース欄に入力見出しと内容を表示する", () => {
    const node: ExecutionNodeDTO = { ...baseNode, input: { seed: true } };
    render(<NodeDetail {...defaultProps} node={node} />);
    fireEvent.click(screen.getByText(uiText.nodeDetail.trace.inputHeading));
    expect(screen.getByText(uiText.nodeDetail.trace.inputHeading)).toBeInTheDocument();
    expect(screen.getByText(/"seed": true/, { exact: false })).toBeInTheDocument();
  });

  describe("Wait / Resume 詳細", () => {
    it("status が WAITING のとき「待機中 (Wait)」と理由を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.waiting.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.waiting.reasonWaitByWaitKeyAndResumeWait)).toBeInTheDocument();
    });

    it("status が WAITING で resumeEventName を渡すと「再開 イベント名」を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} resumeEventName="DoneC" />);
      expect(screen.getByText(uiText.nodeDetail.waiting.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.waiting.resumeEventName("DoneC"))).toBeInTheDocument();
    });

    it("status が WAITING で resumeEventName が空のときイベント名行を表示しない", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} resumeEventName="" />);
      expect(screen.getByText(uiText.nodeDetail.waiting.title)).toBeInTheDocument();
      expect(screen.queryByText(new RegExp(`^${uiText.nodeDetail.waiting.resumeEventName("")}`))).not.toBeInTheDocument();
    });

    it("status が RUNNING のとき「待機中」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText(uiText.nodeDetail.waiting.title)).not.toBeInTheDocument();
    });
  });

  describe("Cancel 詳細", () => {
    it("status が CANCELED で cancelReason ありのとき「キャンセル 詳細」と reason を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "CANCELED",
        canceledByExecution: false,
        cancelReason: "user"
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel))).toBeInTheDocument();
      expect(screen.getByText(/reason: user/)).toBeInTheDocument();
    });

    it("status が CANCELED で canceledByExecution が true のとき「実行 キャンセル により収束」を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "CANCELED",
        canceledByExecution: true
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel))).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.cancel.convergedByExecutionCancel)).toBeInTheDocument();
    });

    it("status が RUNNING のとき「キャンセル 詳細」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText(uiText.nodeDetail.cancel.detailTitle(uiText.actions.cancel))).not.toBeInTheDocument();
    });
  });

  describe("失敗情報", () => {
    it("status が FAILED で error.message ありのとき「失敗情報」とメッセージを表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "FAILED",
        error: { message: "Something went wrong" }
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.failure.title)).toBeInTheDocument();
      expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    });

    it("status が FAILED で error.message が空のとき「（メッセージなし）」を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "FAILED",
        error: { message: "" }
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.failure.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.failure.noMessage)).toBeInTheDocument();
    });

    it("status が FAILED で error がないとき「（メッセージなし）」を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "FAILED" };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText(uiText.nodeDetail.failure.title)).toBeInTheDocument();
      expect(screen.getByText(uiText.nodeDetail.failure.noMessage)).toBeInTheDocument();
    });

    it("status が RUNNING のとき「失敗情報」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText(uiText.nodeDetail.failure.title)).not.toBeInTheDocument();
    });
  });

  describe("Resume ボタン", () => {
    it("resumeDisabledReason が null のとき再開ボタンが有効", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} />);
      const button = screen.getByRole("button", { name: uiText.actions.resume });
      expect(button).not.toBeDisabled();
    });

    it("resumeDisabledReason があるときその理由を表示しボタンは無効", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(
        <NodeDetail
          {...defaultProps}
          node={node}
          resumeDisabledReason="WAITING 状態のノードのみ Resume できます"
        />
      );
      expect(screen.getByText(/WAITING 状態のノードのみ/)).toBeInTheDocument();
      const button = screen.getByRole("button", { name: uiText.actions.resume });
      expect(button).toBeDisabled();
    });

    it("再開ボタンクリックで onResume が呼ばれる", () => {
      const onResume = vi.fn();
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} onResume={onResume} />);
      fireEvent.click(screen.getByRole("button", { name: uiText.actions.resume }));
      expect(onResume).toHaveBeenCalledTimes(1);
    });
  });
});
