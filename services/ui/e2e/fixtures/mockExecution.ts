import type { ExecutionDTO } from "../../app/lib/types";

export const mockExecution: ExecutionDTO = {
  executionId: "ex-1",
  status: "ACTIVE",
  graphId: "hello",
  cancelRequestedAt: null,
  canceledAt: null,
  failedAt: null,
  completedAt: null,
  nodes: [
    { nodeId: "start", nodeType: "Start", status: "SUCCEEDED", attempt: 1, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-a", nodeType: "Task", status: "RUNNING", attempt: 1, workerId: "w1", waitKey: null, canceledByExecution: false },
    { nodeId: "fork-1", nodeType: "Fork", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-b", nodeType: "Task", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "task-c", nodeType: "Wait", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "join-1", nodeType: "Join", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
    { nodeId: "success", nodeType: "Success", status: "IDLE", attempt: 0, workerId: null, waitKey: null, canceledByExecution: false },
  ],
};
