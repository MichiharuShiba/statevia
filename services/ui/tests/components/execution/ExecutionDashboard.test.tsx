import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { ExecutionDashboard } from "../../../app/components/execution/ExecutionDashboard";
import { renderWithUiText } from "../../testUtils";
import type { WorkflowView } from "../../../app/lib/types";

const workflowView = (): WorkflowView => ({
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

vi.mock("../../../app/features/graph/useGraphData", () => ({
  useGraphData: () => ({ nodes: [], edges: [] }),
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
    vi.mocked(useExecution).mockReturnValue({
      execution: workflowView(),
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

  it("初期 executionId でヘッダを描画する", async () => {
    renderWithUiText(<ExecutionDashboard initialExecutionId="ex-1" autoLoadOnMount={false} />);

    await waitFor(() => {
      expect(screen.getByLabelText(/実行 ID/i)).toBeInTheDocument();
    });
  });
});
