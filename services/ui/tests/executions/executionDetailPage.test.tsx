import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import ExecutionDetailPage from "../../app/executions/[executionId]/page";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useParams: () => ({ executionId: "ex-1" }),
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("../../app/features/execution/useExecution", () => ({
  useExecution: vi.fn()
}));

vi.mock("../../app/features/execution/useExecutionEvents", () => ({
  useExecutionEvents: () => ({ events: [], loading: false, error: null, loadMore: vi.fn(), hasMore: false })
}));

vi.mock("../../app/features/execution/useExecutionStateAtSeq", () => ({
  useExecutionStateAtSeq: () => ({
    replayExecution: null,
    replayLoading: false,
    replayError: null,
    clearReplay: vi.fn(),
    loadStateAtSeq: vi.fn()
  })
}));

vi.mock("../../app/features/graph/useGraphDefinition", () => ({
  useGraphDefinition: () => ({ definition: null, loading: false, error: null })
}));

vi.mock("../../app/features/graph/useGraphData", () => ({
  useGraphData: () => ({ nodes: [], edges: [] }),
  getNodeWithFallback: vi.fn()
}));

vi.mock("../../app/features/nodes/useNodeCommands", () => ({
  useNodeCommands: () => ({ resumeNode: vi.fn(), cancelNode: vi.fn(), publishEvent: vi.fn() }),
  getResumeDisabledReason: () => null
}));

vi.mock("../../app/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../app/lib/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { useExecution } from "../../app/features/execution/useExecution";

describe("ExecutionDetailPage", () => {
  beforeEach(() => {
    vi.mocked(useExecution).mockReturnValue({
      execution: {
        displayId: "ex-1",
        resourceId: "wf-1",
        graphId: "g-1",
        status: "Running",
        startedAt: "2026-01-01T00:00:00Z",
        cancelRequested: false,
        restartLost: false,
        nodes: []
      },
      loading: false,
      canCancel: false,
      terminal: false,
      loadExecution: vi.fn(),
      cancelExecution: vi.fn(),
      publishEvent: vi.fn(),
      selectedNodeId: null,
      setSelectedNodeId: vi.fn()
    });
  });

  it("ワークフロー ID 付きで実行ダッシュボードを描画する", async () => {
    renderWithUiText(<ExecutionDetailPage />);

    await waitFor(() => {
      expect(screen.getByText("ex-1")).toBeInTheDocument();
    });
  });
});
