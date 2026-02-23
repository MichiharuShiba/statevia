/**
 * ExecutionRepository
 * ExecutionState の永続化を担当
 */
import { PoolClient } from "pg";
import { pool } from "../db.js";
import { ExecutionState } from "../../../domain/value-objects/execution-state.js";
import { Node as NodeEntity } from "../../../domain/entities/node.js";
import { DomainError } from "../../../domain/errors.js";

export class ExecutionRepository {
  /**
   * ExecutionState をロード（参照用・ロックなし）。存在しなければ null。
   */
  static async loadOptional(executionId: string): Promise<ExecutionState | null> {
    const client = await pool.connect();
    try {
      const state = await this.loadInternal(client, executionId, false);
      return state;
    } finally {
      client.release();
    }
  }

  /**
   * ExecutionState をロード（FOR UPDATE ロック付き）。存在しなければ DomainError NOT_FOUND。
   */
  static async loadWithLock(client: PoolClient, executionId: string): Promise<ExecutionState> {
    const state = await this.loadInternal(client, executionId, true);
    if (!state) throw new DomainError("NOT_FOUND", "Execution not found", { executionId });
    return state;
  }

  private static readonly SELECT_EXECUTION =
    "select execution_id, graph_id, status, cancel_requested_at, canceled_at, failed_at, completed_at, version from executions where execution_id = $1";

  private static readonly SELECT_EXECUTION_FOR_UPDATE =
    "select execution_id, graph_id, status, cancel_requested_at, canceled_at, failed_at, completed_at, version from executions where execution_id = $1 FOR UPDATE";

  private static async loadInternal(
    client: PoolClient,
    executionId: string,
    forUpdate: boolean
  ): Promise<ExecutionState | null> {
    const ex = await client.query(
      forUpdate ? ExecutionRepository.SELECT_EXECUTION_FOR_UPDATE : ExecutionRepository.SELECT_EXECUTION,
      [executionId]
    );
    if (ex.rowCount === 0) return null;

    const nodes = await client.query(
      `select node_id, node_type, status, attempt, worker_id, wait_key, output, error, canceled_by_execution
         from node_states where execution_id = $1`,
      [executionId]
    );

    const nodeMap: Record<string, NodeEntity> = {};
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

  /**
   * ExecutionState を保存
   */
  static async save(client: PoolClient, s: ExecutionState): Promise<void> {
    await this.upsertExecution(client, s);
    await this.upsertNodes(client, s);
  }

  private static async upsertExecution(client: PoolClient, s: ExecutionState): Promise<void> {
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

  private static async upsertNodes(client: PoolClient, s: ExecutionState): Promise<void> {
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
}
