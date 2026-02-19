/**
 * Node エンティティ
 * ワークフロー内のノード（状態）を表す
 */
export type NodeStatus =
  | "IDLE"
  | "READY"
  | "RUNNING"
  | "WAITING"
  | "SUCCEEDED"
  | "FAILED"
  | "CANCELED";

export type Node = {
  nodeId: string;
  nodeType: string;
  status: NodeStatus;
  attempt: number;
  workerId?: string;
  waitKey?: string;
  output?: unknown;
  error?: unknown;
  canceledByExecution?: boolean;
};
