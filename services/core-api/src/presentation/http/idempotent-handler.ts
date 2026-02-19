/**
 * Idempotent Handler
 * 冪等性を保証するHTTPハンドラー
 */
import express from "express";
import { IdempotencyService } from "../../infrastructure/idempotency/idempotency-service.js";
import { endpointKey } from "./middleware.js";

export async function idempotentHandler<T>(
  req: express.Request,
  handler: () => Promise<{ status: number; body: T }>
): Promise<{ status: number; body: T }> {
  const idempotencyKey = req.header("X-Idempotency-Key");
  if (!idempotencyKey) {
    throw new Error("X-Idempotency-Key header is required");
  }

  const endpoint = endpointKey(req);
  const requestHash = IdempotencyService.hashRequest(req.body);

  const replay = await IdempotencyService.enforceOrReplay({
    idempotencyKey,
    endpoint,
    requestHash
  });

  if (replay) {
    return replay as { status: number; body: T };
  }

  const result = await handler();
  await IdempotencyService.save(idempotencyKey, endpoint, requestHash, result.status, result.body);
  return result;
}
