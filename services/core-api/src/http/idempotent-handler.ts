import express from "express";
import { enforceIdempotencyOrReplay, hashRequest, saveIdempotency } from "./idempotency.js";
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
  const requestHash = hashRequest(req.body);

  const replay = await enforceIdempotencyOrReplay({
    idempotencyKey,
    endpoint,
    requestHash
  });

  if (replay) {
    return replay as { status: number; body: T };
  }

  const result = await handler();
  await saveIdempotency(idempotencyKey, endpoint, requestHash, result.status, result.body);
  return result;
}
