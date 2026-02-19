import { PoolClient } from "pg";
import { pool } from "../store/db.js";
import { loadExecutionStateTx } from "../store/load.js";
import { appendEventsTx } from "../store/eventStore.js";
import { upsertExecutionStateTx, upsertNodeStatesTx } from "../store/projections.js";
import { ExecutionState, EventEnvelope, Actor } from "./types.js";
import { applyEvents } from "./commands.js";

function nowIso() {
  return new Date().toISOString();
}

function mkEvent(executionId: string, type: string, actor: Actor, payload: any, correlationId?: string): EventEnvelope {
  return {
    eventId: crypto.randomUUID(),
    executionId,
    type,
    occurredAt: nowIso(),
    actor,
    correlationId,
    schemaVersion: 1,
    payload
  };
}

// Node 20+ なら global crypto がある
declare const crypto: { randomUUID(): string };

/**
 * Cancel 収束を非同期で処理する Orchestrator
 * Cancel リクエスト後、実際に実行が停止するまで待機し、EXECUTION_CANCELED イベントを発行する
 */
export async function convergeCancelAsync(executionId: string, actor: Actor, reason?: string, correlationId?: string): Promise<void> {
  const client = await pool.connect();
  try {
    await client.query("begin");
    
    // FOR UPDATE でロックを取得
    const state = await loadExecutionStateTx(client, executionId);
    
    // 既に確定済みなら何もしない
    if (["COMPLETED", "FAILED", "CANCELED"].includes(state.status)) {
      await client.query("commit");
      return;
    }
    
    // Cancel リクエストがなければ何もしない
    if (!state.cancelRequestedAt) {
      await client.query("commit");
      return;
    }
    
    // 実行中のノードがあるかチェック
    const hasRunningNodes = Object.values(state.nodes).some(
      (node) => node.status === "RUNNING" || node.status === "WAITING"
    );
    
    // 実行中のノードがなく、まだ確定していない場合のみ CANCELED を発行
    if (!hasRunningNodes && !state.canceledAt) {
      const cancelEvent = mkEvent(
        executionId,
        "EXECUTION_CANCELED",
        actor,
        { reason: reason ?? null },
        correlationId
      );
      
      const newState = applyEvents(state, [cancelEvent]);
      
      await appendEventsTx(client, [cancelEvent]);
      await upsertExecutionStateTx(client, newState);
      await upsertNodeStatesTx(client, newState);
    }
    
    await client.query("commit");
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }
}
