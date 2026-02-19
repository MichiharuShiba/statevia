import express from "express";
import { pool } from "../../store/db.js";
import { loadExecutionState } from "../../store/load.js";
import { executeCommandTx } from "../transaction.js";
import { idempotentHandler } from "../idempotent-handler.js";
import { actorFromReq } from "../middleware.js";
import { createExecutionSchema, cancelExecutionSchema } from "../validators.js";
import { appendEventsTx } from "../../store/eventStore.js";
import { upsertExecutionStateTx, upsertNodeStatesTx } from "../../store/projections.js";
import {
  cmdCreateExecution,
  cmdStartExecution,
  cmdCancelExecution,
  applyEvents
} from "../../core/commands.js";
import { convergeCancelAsync } from "../../core/orchestrator.js";
import { ExecutionState } from "../../core/types.js";

export const executionsRouter = express.Router();

// Create Execution
executionsRouter.post("/", async (req, res, next) => {
  try {
    const body = createExecutionSchema.parse(req.body);
    const executionId = body.executionId ?? crypto.randomUUID();

    const result = await idempotentHandler(req, async () => {
      // state を読む（簡易：node無し想定）
      const initialState: ExecutionState = {
        executionId,
        graphId: body.graphId,
        status: "ACTIVE",
        version: 0,
        nodes: {}
      };

      const { events } = cmdCreateExecution({
        executionId,
        graphId: body.graphId,
        input: body.input,
        actor: actorFromReq(req),
        correlationId: req.header("X-Correlation-Id") ?? undefined
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
          [executionId, body.graphId]
        );

        await appendEventsTx(client, events);
        await upsertExecutionStateTx(client, newState);
        await upsertNodeStatesTx(client, newState);

        await client.query("commit");

        return {
          status: 202,
          body: {
            executionId,
            command: "CreateExecution",
            accepted: true,
            correlationId: req.header("X-Correlation-Id") ?? null,
            idempotencyKey: req.header("X-Idempotency-Key")!
          }
        };
      } catch (error) {
        await client.query("rollback");
        throw error;
      } finally {
        client.release();
      }
    });

    res.status(result.status).json(result.body);
  } catch (e) {
    next(e);
  }
});

// Start Execution
executionsRouter.post("/:executionId/start", async (req, res, next) => {
  try {
    const { executionId } = req.params;

    const result = await idempotentHandler(req, async () => {
      const s1 = await executeCommandTx({
        executionId,
        commandFn: (state) => cmdStartExecution(state, actorFromReq(req), req.header("X-Correlation-Id") ?? undefined)
      });

      return {
        status: 202,
        body: {
          executionId,
          command: "StartExecution",
          accepted: true,
          correlationId: req.header("X-Correlation-Id") ?? null,
          idempotencyKey: req.header("X-Idempotency-Key")!
        }
      };
    });

    res.status(result.status).json(result.body);
  } catch (e) {
    next(e);
  }
});

// Cancel Execution
executionsRouter.post("/:executionId/cancel", async (req, res, next) => {
  try {
    const { executionId } = req.params;
    const body = cancelExecutionSchema.parse(req.body);

    const result = await idempotentHandler(req, async () => {
      const actor = actorFromReq(req);
      const correlationId = req.header("X-Correlation-Id") ?? undefined;
      
      // Cancel リクエストを発行（即座に確定）
      const s1 = await executeCommandTx({
        executionId,
        commandFn: (state) => cmdCancelExecution(state, actor, body?.reason, correlationId)
      });

      // Cancel 収束を非同期で処理（レスポンスは即座に返す）
      convergeCancelAsync(executionId, actor, body?.reason, correlationId).catch((err) => {
        // エラーログを出力（本番環境では適切なロギングシステムを使用）
        console.error(`Failed to converge cancel for execution ${executionId}:`, err);
      });

      return {
        status: 202,
        body: {
          executionId,
          command: "CancelExecution",
          accepted: true,
          correlationId: correlationId ?? null,
          idempotencyKey: req.header("X-Idempotency-Key")!
        }
      };
    });

    res.status(result.status).json(result.body);
  } catch (e) {
    next(e);
  }
});

// Get Execution
executionsRouter.get("/:executionId", async (req, res, next) => {
  try {
    const s = await loadExecutionState(req.params.executionId);
    res.json({
      executionId: s.executionId,
      status: s.status,
      graphId: s.graphId,
      cancelRequestedAt: s.cancelRequestedAt ?? null,
      canceledAt: s.canceledAt ?? null,
      failedAt: s.failedAt ?? null,
      completedAt: s.completedAt ?? null,
      nodes: Object.values(s.nodes).map((n) => ({
        nodeId: n.nodeId,
        nodeType: n.nodeType,
        status: n.status,
        attempt: n.attempt,
        workerId: n.workerId ?? null,
        waitKey: n.waitKey ?? null,
        canceledByExecution: n.canceledByExecution ?? false
      }))
    });
  } catch (e) {
    next(e);
  }
});

// Node 20+ なら global crypto がある
declare const crypto: { randomUUID(): string };
