import { pool } from "./db.js";
import { ExecutionState, NodeState } from "../core/types.js";
import { notFound } from "../http/errors.js";

export async function loadExecutionState(executionId: string): Promise<ExecutionState> {
  const ex = await pool.query(
    `select execution_id, graph_id, status,
            cancel_requested_at, canceled_at, failed_at, completed_at, version
       from executions where execution_id = $1`,
    [executionId]
  );
  if (ex.rowCount === 0) throw notFound("Execution not found", { executionId });

  const nodes = await pool.query(
    `select node_id, node_type, status, attempt, worker_id, wait_key, output, error, canceled_by_execution
       from node_states where execution_id = $1`,
    [executionId]
  );

  const nodeMap: Record<string, NodeState> = {};
  for (const r of nodes.rows) {
    nodeMap[r.node_id] = {
      nodeId: r.node_id,
      nodeType: r.node_type,
      status: r.status,
      attempt: r.attempt,
      workerId: r.worker_id ?? undefined,
      waitKey: r.wait_key ?? undefined,
      output: r.output ?? undefined,
      error: r.error ?? undefined,
      canceledByExecution: r.canceled_by_execution ?? false
    };
  }

  return {
    executionId: ex.rows[0].execution_id,
    graphId: ex.rows[0].graph_id,
    status: ex.rows[0].status,
    cancelRequestedAt: ex.rows[0].cancel_requested_at ?? undefined,
    canceledAt: ex.rows[0].canceled_at ?? undefined,
    failedAt: ex.rows[0].failed_at ?? undefined,
    completedAt: ex.rows[0].completed_at ?? undefined,
    version: ex.rows[0].version,
    nodes: nodeMap
  };
}