import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import ExecutionGraphPage from "../../app/executions/[executionId]/graph/page";
import { buildUseExecutionMock } from "../mocks/useExecutionMock";
import { renderWithUiText } from "../testUtils";

vi.mock("next/navigation", () => ({
  useParams: () => ({ executionId: "ex-1" }),
  useRouter: () => ({ push: vi.fn() })
}));

vi.mock("../../features/executions/hooks/useExecution", () => ({
  useExecution: vi.fn()
}));

vi.mock("../../features/executions/hooks/useExecutionEvents", () => ({
  useExecutionEvents: () => ({ events: [], loading: false, error: null, loadMore: vi.fn(), hasMore: false })
}));

vi.mock("../../features/executions/hooks/useExecutionStateAtSeq", () => ({
  useExecutionStateAtSeq: () => ({
    replayExecution: null,
    replayLoading: false,
    replayError: null,
    clearReplay: vi.fn(),
    loadStateAtSeq: vi.fn()
  })
}));

vi.mock("../../features/executions/hooks/useGraphDefinition", () => ({
  useGraphDefinition: () => ({ definition: null, loading: false, error: null })
}));

vi.mock("../../features/executions/hooks/useGraphData", () => ({
  useGraphData: () => ({
    nodes: [
      {
        nodeId: "n-1",
        executionNodeId: "n-1",
        nodeName: "task",
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

vi.mock("../../features/executions/hooks/useNodeCommands", () => ({
  useNodeCommands: () => ({ resumeNode: vi.fn(), cancelNode: vi.fn(), publishEvent: vi.fn() }),
  getResumeDisabledReason: () => null
}));

vi.mock("@/shared/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api")>();
  return { ...actual, apiGet: vi.fn() };
});

import { useExecution } from "../../features/executions/hooks/useExecution";

describe("ExecutionGraphPage", () => {
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
    renderWithUiText(<ExecutionGraphPage />);

    await waitFor(() => {
      expect(screen.getByText("Task")).toBeInTheDocument();
    });
  });
});
