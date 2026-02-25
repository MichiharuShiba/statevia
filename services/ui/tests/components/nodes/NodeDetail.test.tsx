import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { NodeDetail } from "../../../app/components/nodes/NodeDetail";
import type { ExecutionDTO, ExecutionNodeDTO } from "../../../app/lib/types";

const baseExecution: ExecutionDTO = {
  executionId: "ex-1",
  status: "ACTIVE",
  graphId: "g-1",
  cancelRequestedAt: null,
  canceledAt: null,
  failedAt: null,
  completedAt: null,
  nodes: []
};

const baseNode: ExecutionNodeDTO = {
  nodeId: "n-1",
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
  it("execution が null のとき「Execution を読み込んでください」を表示する", () => {
    render(
      <NodeDetail
        {...defaultProps}
        execution={null}
        node={baseNode}
      />
    );
    expect(screen.getByText("Execution を読み込んでください。")).toBeInTheDocument();
  });

  it("node が null のとき「Node を選択してください」を表示する", () => {
    render(
      <NodeDetail
        {...defaultProps}
        execution={baseExecution}
        node={null}
      />
    );
    expect(screen.getByText("Node を選択してください。")).toBeInTheDocument();
  });

  it("node が選択されているとき Node Detail と nodeId を表示する", () => {
    render(<NodeDetail {...defaultProps} />);
    expect(screen.getByText("Node Detail")).toBeInTheDocument();
    expect(screen.getByText("n-1")).toBeInTheDocument();
    expect(screen.getByText("RUNNING")).toBeInTheDocument();
  });

  describe("Wait / Resume 詳細", () => {
    it("status が WAITING のとき「待機中 (Wait)」と理由を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText("待機中 (Wait)")).toBeInTheDocument();
      expect(screen.getByText(/理由: waitKey により Resume 待ち/)).toBeInTheDocument();
    });

    it("status が WAITING で resumeEventName を渡すと「Resume イベント名」を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} resumeEventName="DoneC" />);
      expect(screen.getByText("待機中 (Wait)")).toBeInTheDocument();
      expect(screen.getByText(/Resume イベント名: DoneC/)).toBeInTheDocument();
    });

    it("status が WAITING で resumeEventName が空のときイベント名行を表示しない", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} resumeEventName="" />);
      expect(screen.getByText("待機中 (Wait)")).toBeInTheDocument();
      expect(screen.queryByText(/Resume イベント名:/)).not.toBeInTheDocument();
    });

    it("status が RUNNING のとき「待機中」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText("待機中 (Wait)")).not.toBeInTheDocument();
    });
  });

  describe("Cancel 詳細", () => {
    it("status が CANCELED で cancelReason ありのとき「Cancel 詳細」と reason を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "CANCELED",
        canceledByExecution: false,
        cancelReason: "user"
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText("Cancel 詳細")).toBeInTheDocument();
      expect(screen.getByText(/reason: user/)).toBeInTheDocument();
    });

    it("status が CANCELED で canceledByExecution が true のとき「Execution Cancel により収束」を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "CANCELED",
        canceledByExecution: true
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText("Cancel 詳細")).toBeInTheDocument();
      expect(screen.getByText("Execution Cancel により収束")).toBeInTheDocument();
    });

    it("status が RUNNING のとき「Cancel 詳細」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText("Cancel 詳細")).not.toBeInTheDocument();
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
      expect(screen.getByText("失敗情報")).toBeInTheDocument();
      expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    });

    it("status が FAILED で error.message が空のとき「（メッセージなし）」を表示する", () => {
      const node: ExecutionNodeDTO = {
        ...baseNode,
        status: "FAILED",
        error: { message: "" }
      };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText("失敗情報")).toBeInTheDocument();
      expect(screen.getByText("（メッセージなし）")).toBeInTheDocument();
    });

    it("status が FAILED で error がないとき「（メッセージなし）」を表示する", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "FAILED" };
      render(<NodeDetail {...defaultProps} node={node} />);
      expect(screen.getByText("失敗情報")).toBeInTheDocument();
      expect(screen.getByText("（メッセージなし）")).toBeInTheDocument();
    });

    it("status が RUNNING のとき「失敗情報」を表示しない", () => {
      render(<NodeDetail {...defaultProps} />);
      expect(screen.queryByText("失敗情報")).not.toBeInTheDocument();
    });
  });

  describe("Resume ボタン", () => {
    it("resumeDisabledReason が null のとき Resume ボタンが有効", () => {
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} />);
      const button = screen.getByRole("button", { name: "Resume" });
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
      const button = screen.getByRole("button", { name: "Resume" });
      expect(button).toBeDisabled();
    });

    it("Resume クリックで onResume が呼ばれる", () => {
      const onResume = vi.fn();
      const node: ExecutionNodeDTO = { ...baseNode, status: "WAITING", waitKey: "wk-1" };
      render(<NodeDetail {...defaultProps} node={node} onResume={onResume} />);
      fireEvent.click(screen.getByRole("button", { name: "Resume" }));
      expect(onResume).toHaveBeenCalledTimes(1);
    });
  });
});
