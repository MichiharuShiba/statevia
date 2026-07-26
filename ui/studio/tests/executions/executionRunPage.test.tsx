import { describe, expect, it, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import ExecutionRunPage from "../../app/executions/[executionId]/run/page";
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
  useGraphData: () => ({ nodes: [], edges: [], groups: [], mergedNodes: [], graphId: "g-1", definitionBased: false }),
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

describe("ExecutionRunPage", () => {
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

  it("Run 画面で実行 ID を表示する", async () => {
    renderWithUiText(<ExecutionRunPage />);

    await waitFor(() => {
      expect(screen.getByText("ex-1")).toBeInTheDocument();
    });
  });
});
