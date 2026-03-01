/**
 * Executions HTTP Routes
 */
import express from "express";
import { idempotentHandler } from "../idempotent-handler.js";
import { actorFromReq } from "../middleware.js";
import { notFound } from "../errors.js";
import { createExecutionSchema, cancelExecutionSchema } from "../../dto/validators.js";
import { createExecutionUseCase } from "../../../application/use-cases/create-execution-use-case.js";
import { getExecutionUseCase } from "../../../application/use-cases/get-execution-use-case.js";
import { toExecutionReadModel } from "../../../domain/read-model/execution-read-model.js";
import { executeCommandUseCase } from "../../../application/use-cases/execute-command-use-case.js";
import { convergeCancelAsync } from "../../../application/services/orchestrator.js";
import {
  cmdStartExecution,
  cmdCancelExecution
} from "../../../application/commands/command-handlers.js";
import { streamExecutionEvents } from "../stream-execution-handler.js";
import { EventStore } from "../../../infrastructure/persistence/repositories/event-store.js";
import { mapPersistedEventToStreamEvent } from "../stream-events.js";
import { getExecutionStateAtSeqUseCase } from "../../../application/use-cases/get-execution-state-at-seq-use-case.js";

const DEFAULT_EVENTS_LIMIT = 500;
const MAX_EVENTS_LIMIT = 2000;

export const executionsRouter = express.Router();

// Create Execution
executionsRouter.post("/", async (req, res, next) => {
  try {
    const body = createExecutionSchema.parse(req.body);
    const executionId = body.executionId ?? crypto.randomUUID();

    const result = await idempotentHandler(req, async () => {
      const response = await createExecutionUseCase({
        executionId,
        graphId: body.graphId,
        input: body.input,
        actor: actorFromReq(req),
        correlationId: req.header("X-Correlation-Id") ?? undefined
      });
      return {
        status: response.status,
        body: {
          ...response.body,
          idempotencyKey: req.header("X-Idempotency-Key")!
        }
      };
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
      await executeCommandUseCase({
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
      await executeCommandUseCase({
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

// List Execution Events (timeline / replay)
executionsRouter.get("/:executionId/events", async (req, res, next) => {
  try {
    const { executionId } = req.params;
    const state = await getExecutionUseCase(executionId);
    if (!state) throw notFound("Execution not found", { executionId });

    const afterSeqParam = req.query.afterSeq;
    const afterSeq =
      afterSeqParam !== undefined && afterSeqParam !== null ? Number(afterSeqParam) : 0;
    const effectiveAfterSeq = Number.isInteger(afterSeq) && afterSeq >= 0 ? afterSeq : 0;

    const limitParam = req.query.limit;
    const rawLimit =
      limitParam !== undefined && limitParam !== null ? Number(limitParam) : DEFAULT_EVENTS_LIMIT;
    const limit = Math.min(
      Math.max(1, Number.isFinite(rawLimit) ? rawLimit : DEFAULT_EVENTS_LIMIT),
      MAX_EVENTS_LIMIT
    );

    const persisted = await EventStore.listSince(executionId, effectiveAfterSeq, limit);
    const events = persisted
      .map((e) => {
        const stream = mapPersistedEventToStreamEvent(e);
        if (!stream) return null;
        return { seq: e.seq, ...stream };
      })
      .filter((e): e is NonNullable<typeof e> => e !== null && e !== undefined);

    const hasMore = events.length >= limit;

    res.json({ events, hasMore });
  } catch (e) {
    next(e);
  }
});

// Get Execution State at Seq (replay for timeline)
executionsRouter.get("/:executionId/state", async (req, res, next) => {
  try {
    const { executionId } = req.params;
    const atSeqParam = req.query.atSeq;
    const atSeq =
      atSeqParam !== undefined && atSeqParam !== null ? Number(atSeqParam) : undefined;
    if (atSeq === undefined || !Number.isInteger(atSeq) || atSeq < 1) {
      res.status(400).json({
        error: { code: "BAD_REQUEST", message: "Query atSeq is required and must be a positive integer" }
      });
      return;
    }

    const s = await getExecutionStateAtSeqUseCase(executionId, atSeq);
    if (!s) {
      throw notFound("Execution not found or no events at seq", {
        executionId,
        atSeq
      });
    }

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
        canceledByExecution: n.canceledByExecution ?? false,
        error: n.error ?? null
      }))
    });
  } catch (e) {
    next(e);
  }
});

// Get Execution（UI向け Read Model: data-integration-contract §2.1）
executionsRouter.get("/:executionId", async (req, res, next) => {
  try {
    const s = await getExecutionUseCase(req.params.executionId);
    if (!s) throw notFound("Execution not found", { executionId: req.params.executionId });
    res.json(toExecutionReadModel(s));
  } catch (e) {
    next(e);
  }
});

// Stream Execution Events (SSE)
executionsRouter.get("/:executionId/stream", async (req, res, next) => {
  try {
    const { executionId } = req.params;
    const state = await getExecutionUseCase(executionId);
    if (!state) throw notFound("Execution not found", { executionId });
    await streamExecutionEvents(executionId, res, req);
  } catch (e) {
    next(e);
  }
});

// Node 20+ なら global crypto がある
declare const crypto: { randomUUID(): string };
