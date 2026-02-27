/**
 * GetExecutionStateAtSeq ユースケース
 * 指定 seq 時点までのイベントをリプレイして状態を返す（タイムライン/リプレイ用）
 */
import { EventStore } from "../../infrastructure/persistence/repositories/event-store.js";
import { getExecutionUseCase } from "./get-execution-use-case.js";
import { reduce } from "../../domain/domain-services/reducer.js";
import type { ExecutionState } from "../../domain/value-objects/execution-state.js";
import type { EventEnvelope } from "../../domain/value-objects/event-envelope.js";
import type { PersistedEvent } from "../../infrastructure/persistence/repositories/event-store.js";

function toEventEnvelope(persisted: PersistedEvent): EventEnvelope {
  return {
    eventId: persisted.eventId,
    executionId: persisted.executionId,
    type: persisted.type,
    occurredAt: persisted.occurredAt,
    actor: { kind: "system" },
    schemaVersion: 1,
    payload: persisted.payload
  };
}

function initialExecutionState(executionId: string, graphId: string): ExecutionState {
  return {
    executionId,
    graphId,
    status: "ACTIVE",
    version: 0,
    nodes: {}
  };
}

/**
 * 指定 seq 時点（その seq を含む）までイベントをリプレイした ExecutionState を返す。
 * 実行が存在しない場合や atSeq が 0 の場合は null。
 */
export async function getExecutionStateAtSeqUseCase(
  executionId: string,
  atSeq: number
): Promise<ExecutionState | null> {
  if (atSeq < 1) return null;

  const current = await getExecutionUseCase(executionId);
  if (!current) return null;

  const persisted = await EventStore.listSince(executionId, 0, atSeq);
  if (persisted.length === 0) return null;

  let state = initialExecutionState(executionId, current.graphId);
  for (const e of persisted) {
    if (e.schemaVersion !== 1) continue;
    state = reduce(state, toEventEnvelope(e));
  }

  return state;
}
