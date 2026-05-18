import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import WorkflowGraphPage from "../../app/workflows/[workflowId]/graph/page";
import { buildUseExecutionMock } from "../mocks/useExecutionMock";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useParams: () => ({ workflowId: "ex-1" }),
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
  useGraphData: () => ({
    nodes: [
      {
        nodeId: "n-1",
        executionNodeId: "n-1",
        stateName: "task",
        nodeType: "Task",
        label: "Task",
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
  }),
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

describe("WorkflowGraphPage", () => {
  beforeEach(() => {
    vi.mocked(useExecution).mockReturnValue(
      buildUseExecutionMock({
        displayId: "ex-1",
        resourceId: "wf-1",
        graphId: "g-1",
        status: "Running",
        startedAt: "2026-01-01T00:00:00Z",
        cancelRequested: false,
        restartLost: false,
        nodes: []
      })
    );
  });

  it("Graph 専用モードでノードを描画する", async () => {
    renderWithUiText(<WorkflowGraphPage />);

    await waitFor(() => {
      expect(screen.getByText("Task")).toBeInTheDocument();
    });
  });
});
