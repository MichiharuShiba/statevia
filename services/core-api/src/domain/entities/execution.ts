/**
 * Execution エンティティ
 * ワークフロー実行の集約ルート
 */
export type ExecutionStatus = "ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED";

export type Execution = {
  executionId: string;
  graphId: string;
  status: ExecutionStatus;
  cancelRequestedAt?: string;
  canceledAt?: string;
  failedAt?: string;
  completedAt?: string;
  version: number;
};
