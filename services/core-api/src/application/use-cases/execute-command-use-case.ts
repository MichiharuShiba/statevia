/**
 * ExecuteCommand ユースケース
 * 汎用的なコマンド実行ユースケース
 */
import { pool } from "../../infrastructure/persistence/db.js";
import { EventStore } from "../../infrastructure/persistence/repositories/event-store.js";
import { ExecutionRepository } from "../../infrastructure/persistence/repositories/execution-repository.js";
import { applyEvents } from "../commands/command-handlers.js";
import { ExecutionState } from "../../domain/value-objects/execution-state.js";
import { EventEnvelope } from "../../domain/value-objects/event-envelope.js";

export interface ExecuteCommandRequest {
  executionId: string;
  commandFn: (state: ExecutionState) => { events: EventEnvelope[] };
}

export async function executeCommandUseCase(request: ExecuteCommandRequest): Promise<ExecutionState> {
  const client = await pool.connect();
  try {
    await client.query("begin");
    
    // FOR UPDATE でロックを取得してから状態を読み込む
    const initialState = await ExecutionRepository.loadWithLock(client, request.executionId);
    
    const { events } = request.commandFn(initialState);
    const newState = applyEvents(initialState, events);

    await EventStore.appendEvents(client, events);
    await ExecutionRepository.save(client, newState);
    await client.query("commit");
    
    return newState;
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }
}
