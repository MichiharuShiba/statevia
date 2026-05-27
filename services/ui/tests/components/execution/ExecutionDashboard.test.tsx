import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { ExecutionDashboard } from "../../../app/components/execution/ExecutionDashboard";
import { renderWithUiText } from "../../testUtils";
import type { ExecutionView } from "../../../app/lib/types";

const executionViewFixture = (): ExecutionView => ({
  displayId: "ex-1",
  resourceId: "wf-1",
  graphId: "g-1",
  status: "Running",
  startedAt: "2026-01-01T00:00:00Z",
  cancelRequested: false,
  restartLost: false,
  nodes: []
});

vi.mock("../../../app/features/execution/useExecution", () => ({
  useExecution: vi.fn()
}));

vi.mock("../../../app/features/execution/useExecutionEvents", () => ({
  useExecutionEvents: () => ({
    events: [],
    loading: false,
    error: null,
    loadMore: vi.fn(),
    hasMore: false
  })
}));

vi.mock("../../../app/features/execution/useExecutionStateAtSeq", () => ({
  useExecutionStateAtSeq: () => ({
    replayExecution: null,
    replayLoading: false,
    replayError: null,
    clearReplay: vi.fn(),
    loadStateAtSeq: vi.fn()
  })
}));

vi.mock("../../../app/features/graph/useGraphDefinition", () => ({
  useGraphDefinition: () => ({
    definition: null,
    loading: false,
    error: null
  })
}));

const useGraphDataMock = vi.fn();

vi.mock("../../../app/features/graph/useGraphData", () => ({
  useGraphData: (...args: unknown[]) => useGraphDataMock(...args),
  getNodeWithFallback: vi.fn()
}));

vi.mock("../../../app/features/nodes/useNodeCommands", () => ({
  useNodeCommands: () => ({
    resumeNode: vi.fn(),
    cancelNode: vi.fn(),
    publishEvent: vi.fn()
  }),
  getResumeDisabledReason: () => null
}));

vi.mock("../../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { useExecution } from "../../../app/features/execution/useExecution";

describe("ExecutionDashboard", () => {
  beforeEach(() => {
    useGraphDataMock.mockReturnValue({
      nodes: [
        {
          nodeId: "n-1",
          executionNodeId: "n-1",
          stateName: "task",
          nodeType: "Task",
          label: "Task node",
          status: "RUNNING",
          attempt: 1,
          workerId: null,
          waitKey: null,
          canceledByExecution: false,
          x: 0,
          y: 0,
          w: 200,
          h: 80
        }
      ],
      edges: [],
      groups: [],
      mergedNodes: [],
      graphId: "g-1",
      definitionBased: false
    });
    vi.mocked(useExecution).mockReturnValue({
      execution: executionViewFixture(),
      loading: false,
      canCancel: true,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution: vi.fn(),
      publishEvent: vi.fn(),
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });
  });

  it("初期 executionId でヘッダを描画する", async () => {
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    await waitFor(() => {
      expect(screen.getByLabelText(/実行 ID/i)).toBeInTheDocument();
    });
  });

  it("Cancel ボタンで cancelExecution を呼ぶ", async () => {
    const cancelExecution = vi.fn();
    vi.mocked(useExecution).mockReturnValue({
      execution: executionViewFixture(),
      loading: false,
      canCancel: true,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution,
      publishEvent: vi.fn(),
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });

    renderWithUiText(
      <ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} operationsEnabled />
    );

    fireEvent.click(await screen.findByRole("button", { name: /キャンセル/i }));
    expect(cancelExecution).toHaveBeenCalled();
  });

  it("グラフ表示に切り替えられる", async () => {
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    fireEvent.click(screen.getByRole("button", { name: "グラフ" }));

    await waitFor(() => {
      expect(screen.getByText("Task node")).toBeInTheDocument();
    });
  });
});
