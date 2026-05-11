import { describe, expect, it } from "vitest";
import { buildWorkflowView } from "../../app/lib/workflowView";
import type { WorkflowDTO, WorkflowGraphDTO } from "../../app/lib/types";

describe("buildWorkflowView", () => {
  it("graph の conditionRouting を再評価せずそのまま保持する", () => {
    // Arrange
    const workflow: WorkflowDTO = {
      displayId: "ex-1",
      resourceId: "r-1",
      graphId: "def-1",
      status: "Running",
      startedAt: "2026-01-01T00:00:00Z",
      cancelRequested: false,
      restartLost: false
    };
    const conditionRouting = {
      fact: "Completed",
      resolution: "matched_case",
      matchedCaseIndex: 0,
      caseEvaluations: [{ caseIndex: 0, matched: true }],
      evaluationErrors: []
    };
    const graph: WorkflowGraphDTO = {
      nodes: [
        {
          nodeId: "n-1",
          stateName: "RouteByScore",
          nodeType: "Task",
          startedAt: "2026-01-01T00:00:00Z",
          completedAt: "2026-01-01T00:00:01Z",
          fact: "Completed",
          conditionRouting
        }
      ],
      edges: [{ from: "n-1", to: "n-2", type: 0 }]
    };

    // Act
    const view = buildWorkflowView(workflow, graph);

    // Assert
    expect(view.nodes).toHaveLength(1);
    expect(view.nodes[0]?.executionNodeId).toBe("n-1");
    expect(view.nodes[0]?.nodeType).toBe("Task");
    expect(view.nodes[0]?.conditionRouting).toEqual(conditionRouting);
    expect(view.nodes[0]?.startedAt).toBe("2026-01-01T00:00:00Z");
    expect(view.nodes[0]?.completedAt).toBe("2026-01-01T00:00:01Z");
    expect(view.runtimeEdges).toEqual([{ from: "n-1", to: "n-2", type: 0 }]);
  });

  it("graph の output を ExecutionNodeDTO に引き継ぐ", () => {
    const workflow: WorkflowDTO = {
      displayId: "ex-1",
      resourceId: "r-1",
      graphId: "def-1",
      status: "Running",
      startedAt: "2026-01-01T00:00:00Z",
      cancelRequested: false,
      restartLost: false
    };
    const graph: WorkflowGraphDTO = {
      nodes: [
        {
          nodeId: "n-1",
          stateName: "Task",
          completedAt: "2026-01-01T00:00:02Z",
          fact: "Completed",
          input: { payload: 1 },
          output: { result: 42 },
          attempt: 2,
          workerId: "wk-1",
          waitKey: "resume-key",
          canceledByExecution: true
        }
      ],
      edges: []
    };

    const view = buildWorkflowView(workflow, graph);

    expect(view.nodes[0]?.executionNodeId).toBe("n-1");
    expect(view.nodes[0]?.input).toEqual({ payload: 1 });
    expect(view.nodes[0]?.output).toEqual({ result: 42 });
    expect(view.nodes[0]?.attempt).toBe(2);
    expect(view.nodes[0]?.workerId).toBe("wk-1");
    expect(view.nodes[0]?.waitKey).toBe("resume-key");
    expect(view.nodes[0]?.canceledByExecution).toBe(true);
  });
});
