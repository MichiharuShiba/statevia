/**
 * Execution Read Model（UI向け正規形）
 * data-integration-contract §2.1 に準拠。
 * UI はこの型のフィールドのみを前提に描画する。
 */

export type ExecutionReadModelStatus = "ACTIVE" | "COMPLETED" | "FAILED" | "CANCELED";

export type ExecutionReadModelNodeStatus =
  | "IDLE"
  | "READY"
  | "RUNNING"
  | "WAITING"
  | "SUCCEEDED"
  | "FAILED"
  | "CANCELED";

export type ExecutionReadModelNode = {
  nodeId: string;
  nodeType: string;
  status: ExecutionReadModelNodeStatus;
  attempt: number;
  workerId: string | null;
  waitKey: string | null;
  canceledByExecution: boolean;
  /** 契約 §2.1 の必須外。後方互換のため API は返却してよい。 */
  error?: { message?: string | null } | null;
};

export type ExecutionReadModel = {
  executionId: string;
  graphId: string;
  status: ExecutionReadModelStatus;
  cancelRequestedAt: string | null;
  canceledAt: string | null;
  failedAt: string | null;
  completedAt: string | null;
  nodes: ExecutionReadModelNode[];
};

/**
 * ExecutionState（内部）を ExecutionReadModel（UI向け）に変換する。
 * 契約に含まないフィールド（version, node.output, node.error）は含めない。
 */
export function toExecutionReadModel(state: {
  executionId: string;
  graphId: string;
  status: string;
  cancelRequestedAt?: string | null;
  canceledAt?: string | null;
  failedAt?: string | null;
  completedAt?: string | null;
  nodes: Record<
    string,
    {
      nodeId: string;
      nodeType: string;
      status: string;
      attempt: number;
      workerId?: string | null;
      waitKey?: string | null;
      canceledByExecution?: boolean;
      error?: unknown;
    }
  >;
}): ExecutionReadModel {
  return {
    executionId: state.executionId,
    graphId: state.graphId,
    status: state.status as ExecutionReadModelStatus,
    cancelRequestedAt: state.cancelRequestedAt ?? null,
    canceledAt: state.canceledAt ?? null,
    failedAt: state.failedAt ?? null,
    completedAt: state.completedAt ?? null,
    nodes: Object.values(state.nodes).map((n) => {
      const err = n.error;
      const errorShape =
        err != null && typeof err === "object" && "message" in err
          ? { message: typeof (err as { message?: unknown }).message === "string" ? (err as { message: string }).message : null }
          : null;
      return {
        nodeId: n.nodeId,
        nodeType: n.nodeType,
        status: n.status as ExecutionReadModelNodeStatus,
        attempt: n.attempt,
        workerId: n.workerId ?? null,
        waitKey: n.waitKey ?? null,
        canceledByExecution: n.canceledByExecution ?? false,
        ...(errorShape === null ? {} : { error: errorShape })
      };
    })
  };
}
