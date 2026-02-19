import express from "express";
import { unprocessable } from "./errors.js";

export function actorFromReq(req: express.Request) {
  const kind = (req.header("X-Actor-Kind") ?? "system") as any;
  const id = req.header("X-Actor-Id") ?? undefined;
  return { kind, id };
}

export function endpointKey(req: express.Request): string {
  return `${req.method} ${req.baseUrl}${req.path}`;
}

export function requireIdempotencyKey(req: express.Request, res: express.Response, next: express.NextFunction) {
  const idem = req.header("X-Idempotency-Key");
  if (!idem) {
    throw unprocessable("Missing X-Idempotency-Key");
  }
  next();
}
