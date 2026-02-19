/**
 * Executions HTTP Routes
 */
import express from "express";
import { ExecutionRepository } from "../../../infrastructure/persistence/repositories/execution-repository.js";
import { idempotentHandler } from "../idempotent-handler.js";
import { actorFromReq } from "../middleware.js";
import { createExecutionSchema, cancelExecutionSchema } from "../../dto/validators.js";
import { createExecutionUseCase } from "../../../application/use-cases/create-execution-use-case.js";
import { executeCommandUseCase } from "../../../application/use-cases/execute-command-use-case.js";
import { convergeCancelAsync } from "../../../application/services/orchestrator.js";
import {
  cmdStartExecution,
  cmdCancelExecution
} from "../../../application/commands/command-handlers.js";

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

// Get Execution
executionsRouter.get("/:executionId", async (req, res, next) => {
  try {
    const s = await ExecutionRepository.load(req.params.executionId);
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
