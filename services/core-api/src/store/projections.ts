import { PoolClient } from "pg";
import { ExecutionState } from "../core/types.js";

export async function upsertExecutionStateTx(client: PoolClient, s: ExecutionState) {
  await client.query(
    `insert into executions(
        execution_id, graph_id, status,
        cancel_requested_at, canceled_at, failed_at, completed_at,
        version
     ) values ($1,$2,$3,$4,$5,$6,$7,$8)
     on conflict (execution_id) do update set
       graph_id = excluded.graph_id,
       status = excluded.status,
       cancel_requested_at = excluded.cancel_requested_at,
       canceled_at = excluded.canceled_at,
       failed_at = excluded.failed_at,
       completed_at = excluded.completed_at,
       version = executions.version + 1`,
    [
      s.executionId,
      s.graphId,
      s.status,
      s.cancelRequestedAt ?? null,
      s.canceledAt ?? null,
      s.failedAt ?? null,
      s.completedAt ?? null,
      s.version
    ]
  );
}

export async function upsertNodeStatesTx(client: PoolClient, s: ExecutionState) {
  for (const n of Object.values(s.nodes)) {
    await client.query(
      `insert into node_states(
        execution_id, node_id, node_type, status, attempt,
        worker_id, wait_key, output, error, canceled_by_execution
      ) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
      on conflict (execution_id, node_id) do update set
        node_type = excluded.node_type,
        status = excluded.status,
        attempt = excluded.attempt,
        worker_id = excluded.worker_id,
        wait_key = excluded.wait_key,
        output = excluded.output,
        error = excluded.error,
        canceled_by_execution = excluded.canceled_by_execution`,
      [
        s.executionId,
        n.nodeId,
        n.nodeType,
        n.status,
        n.attempt,
        n.workerId ?? null,
        n.waitKey ?? null,
        n.output ?? null,
        n.error ?? null,
        n.canceledByExecution ?? false
      ]
    );
  }
}