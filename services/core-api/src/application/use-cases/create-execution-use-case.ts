/**
 * CreateExecution ユースケース
 */
import { pool } from "../../infrastructure/persistence/db.js";
import { EventStore } from "../../infrastructure/persistence/repositories/event-store.js";
import { ExecutionRepository } from "../../infrastructure/persistence/repositories/execution-repository.js";
import { cmdCreateExecution, applyEvents } from "../commands/command-handlers.js";
import { Actor } from "../../domain/value-objects/actor.js";
import { ExecutionState } from "../../domain/value-objects/execution-state.js";

export interface CreateExecutionRequest {
  executionId?: string;
  graphId: string;
  input?: any;
  actor: Actor;
  correlationId?: string;
}

export interface CreateExecutionResponse {
  executionId: string;
  command: "CreateExecution";
  accepted: boolean;
  correlationId: string | null;
}

export async function createExecutionUseCase(request: CreateExecutionRequest): Promise<{ status: number; body: CreateExecutionResponse }> {
  const executionId = request.executionId ?? crypto.randomUUID();
  
  // 初期状態を作成
  const initialState: ExecutionState = {
    executionId,
    graphId: request.graphId,
    status: "ACTIVE",
    version: 0,
    nodes: {}
  };

  const { events } = cmdCreateExecution({
    executionId,
    graphId: request.graphId,
    input: request.input,
    actor: request.actor,
    correlationId: request.correlationId
  });

  const newState = applyEvents(initialState, events);

  const client = await pool.connect();
  try {
    await client.query("begin");

    // まず executions 行を確保（未存在なら作る）
    await client.query(
      `insert into executions(execution_id, graph_id, status, version)
       values ($1,$2,'ACTIVE',0)
       on conflict (execution_id) do nothing`,
      [executionId, request.graphId]
    );

    await EventStore.appendEvents(client, events);
    await ExecutionRepository.save(client, newState);

    await client.query("commit");

    return {
      status: 202,
      body: {
        executionId,
        command: "CreateExecution",
        accepted: true,
        correlationId: request.correlationId ?? null
      }
    };
  } catch (error) {
    await client.query("rollback");
    throw error;
  } finally {
    client.release();
  }
}

declare const crypto: { randomUUID(): string };
