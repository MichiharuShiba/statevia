import type { GraphDefinition } from "../types";

export const helloGraphDefinition: GraphDefinition = {
  graphId: "hello",
  nodes: [
    { nodeId: "start", nodeType: "Start", label: "Start" },
    { nodeId: "task-a", nodeType: "Task", label: "Task A" },
    { nodeId: "fork-1", nodeType: "Fork", label: "Fork" },
    { nodeId: "task-b", nodeType: "Task", label: "Task B", branch: "b" },
    { nodeId: "task-c", nodeType: "Wait", label: "Task C", branch: "c" },
    { nodeId: "join-1", nodeType: "Join", label: "Join" },
    { nodeId: "success", nodeType: "Success", label: "Success" }
  ],
  edges: [
    { from: "start", to: "task-a" },
    { from: "task-a", to: "fork-1" },
    { from: "fork-1", to: "task-b", kind: "fork" },
    { from: "fork-1", to: "task-c", kind: "fork" },
    { from: "task-b", to: "join-1", kind: "join" },
    { from: "task-c", to: "join-1", kind: "join" },
    { from: "join-1", to: "success" }
  ],
  groups: [
    {
      groupId: "parallel-1",
      label: "Fork/Join",
      nodeIds: ["fork-1", "task-b", "task-c", "join-1"]
    }
  ],
  layoutHints: {
    direction: "LR",
    branchOrder: ["b", "c"],
    groupPadding: { x: 40, y: 30, header: 28 }
  }
};

