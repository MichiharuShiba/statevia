import { z } from "zod";

export const createExecutionSchema = z.object({
  executionId: z.string().min(1).optional(),
  graphId: z.string().min(1),
  input: z.any().optional()
});

export const cancelExecutionSchema = z.object({
  reason: z.string().optional()
}).optional();

export const createNodeSchema = z.object({
  nodeType: z.string().min(1)
});

export const startNodeSchema = z.object({
  attempt: z.number().int().min(1).default(1),
  workerId: z.string().optional()
});

export const putNodeWaitingSchema = z.object({
  waitKey: z.string().optional(),
  prompt: z.any().optional()
});

export const resumeNodeSchema = z.object({
  resumeKey: z.string().optional()
});
