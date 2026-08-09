import type { GraphDefinition } from "../types";

/** サンプル用の hello 定義グラフ。 */
export const helloGraphDefinition: GraphDefinition = {
  graphId: "hello",
  nodes: [
    { nodeName: "start", nodeType: "Start", label: "Start" },
    { nodeName: "task-a", nodeType: "Task", label: "Task A" },
    { nodeName: "fork-1", nodeType: "Fork", label: "Fork" },
    { nodeName: "task-b", nodeType: "Task", label: "Task B", branch: "b" },
    { nodeName: "task-c", nodeType: "Wait", label: "Task C", branch: "c" },
    { nodeName: "join-1", nodeType: "Join", label: "Join" },
    { nodeName: "success", nodeType: "Success", label: "Success" }
  ],
  edges: [
    { from: "start", to: "task-a" },
    { from: "task-a", to: "fork-1" },
    { from: "fork-1", to: "task-b", kind: "fork" },
    { from: "fork-1", to: "task-c", kind: "fork" },
    { from: "task-b", to: "join-1", kind: "join" },
    { from: "task-c", to: "join-1", kind: "join", edgeType: "Resume", eventName: "DoneC" },
    { from: "join-1", to: "success" }
  ],
  groups: [
    {
      groupId: "parallel-1",
      label: "Fork/Join",
      nodeNames: ["fork-1", "task-b", "task-c", "join-1"]
    }
  ],
  meta: {
    direction: "TB",
    branchOrder: ["b", "c"],
    groupPadding: { x: 40, y: 30, header: 28 }
  }
};

