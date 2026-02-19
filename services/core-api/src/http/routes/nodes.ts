import express from "express";
import { executeCommandTx } from "../transaction.js";
import { idempotentHandler } from "../idempotent-handler.js";
import { actorFromReq } from "../middleware.js";
import {
  createNodeSchema,
  startNodeSchema,
  putNodeWaitingSchema,
  resumeNodeSchema
} from "../validators.js";
import {
  cmdCreateNode,
  cmdStartNode,
  cmdPutNodeWaiting,
  cmdResumeNode
} from "../../core/commands.js";

export const nodesRouter = express.Router();

// Create Node
nodesRouter.post("/executions/:executionId/nodes/:nodeId/create", async (req, res, next) => {
  try {
    const { executionId, nodeId } = req.params;
    const body = createNodeSchema.parse(req.body);

    const result = await idempotentHandler(req, async () => {
      const s1 = await executeCommandTx({
        executionId,
        commandFn: (state) => cmdCreateNode(state, actorFromReq(req), nodeId, body.nodeType, req.header("X-Correlation-Id") ?? undefined)
      });

      return {
        status: 202,
        body: {
          executionId,
          command: "CreateNode",
          accepted: true,
          nodeId,
          idempotencyKey: req.header("X-Idempotency-Key")!
        }
      };
    });

    res.status(result.status).json(result.body);
  } catch (e) {
    next(e);
  }
});

// Start Node
nodesRouter.post("/executions/:executionId/nodes/:nodeId/start", async (req, res, next) => {
  try {
    const { executionId, nodeId } = req.params;
    const body = startNodeSchema.parse(req.body);

    const result = await idempotentHandler(req, async () => {
      const s1 = await executeCommandTx({
        executionId,
        commandFn: (state) => cmdStartNode(state, actorFromReq(req), nodeId, body.attempt, body.workerId, req.header("X-Correlation-Id") ?? undefined)
      });

      return {
        status: 202,
        body: {
          executionId,
          command: "StartNode",
          accepted: true,
          nodeId,
          idempotencyKey: req.header("X-Idempotency-Key")!
        }
      };
    });

    res.status(result.status).json(result.body);
  } catch (e) {
    next(e);
  }
});

// Put Node Waiting
nodesRouter.post("/executions/:executionId/nodes/:nodeId/wait", async (req, res, next) => {
  try {
    const { executionId, nodeId } = req.params;
    const body = putNodeWaitingSchema.parse(req.body);

    const result = await idempotentHandler(req, async () => {
      const s1 = await executeCommandTx({
        executionId,
        commandFn: (state) => cmdPutNodeWaiting(state, actorFromReq(req), nodeId, body.waitKey, body.prompt, req.header("X-Correlation-Id") ?? undefined)
      });

      return {
        status: 202,
        body: {
          executionId,
          command: "PutNodeWaiting",
          accepted: true,
          nodeId,
          idempotencyKey: req.header("X-Idempotency-Key")!
        }
      };
    });

    res.status(result.status).json(result.body);
  } catch (e) {
    next(e);
  }
});

// Resume Node
nodesRouter.post("/executions/:executionId/nodes/:nodeId/resume", async (req, res, next) => {
  try {
    const { executionId, nodeId } = req.params;
    const body = resumeNodeSchema.parse(req.body);

    const result = await idempotentHandler(req, async () => {
      const s1 = await executeCommandTx({
        executionId,
        commandFn: (state) => cmdResumeNode(state, actorFromReq(req), nodeId, body.resumeKey, req.header("X-Correlation-Id") ?? undefined)
      });

      return {
        status: 202,
        body: {
          executionId,
          command: "ResumeNode",
          accepted: true,
          nodeId,
          idempotencyKey: req.header("X-Idempotency-Key")!
        }
      };
    });

    res.status(result.status).json(result.body);
  } catch (e) {
    next(e);
  }
});
