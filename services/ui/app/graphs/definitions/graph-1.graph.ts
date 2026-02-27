import type { GraphDefinition } from "../types";

/** 線形フロー（単一タスク・Wait/Resume 想定）。API テストや HTTP サンプルで利用される graphId。 */
export const graph1GraphDefinition: GraphDefinition = {
  graphId: "graph-1",
  nodes: [
    { nodeId: "start", nodeType: "Start", label: "Start" },
    { nodeId: "task-1", nodeType: "Task", label: "Task 1" },
    { nodeId: "success", nodeType: "Success", label: "Success" }
  ],
  edges: [
    { from: "start", to: "task-1" },
    { from: "task-1", to: "success" }
  ],
  layoutHints: {
    direction: "LR",
    groupPadding: { x: 40, y: 30, header: 28 }
  }
};
