/**
 * HTTP エラーハンドラー
 */
import express from "express";
import { HttpError } from "./errors.js";

function isDomainError(err: unknown): err is { name: "DomainError"; code: string; message: string; details?: Record<string, unknown> } {
  return typeof err === "object" && err !== null && (err as { name?: string }).name === "DomainError";
}

function isZodError(err: unknown): err is { name: "ZodError"; flatten?: () => unknown } {
  return typeof err === "object" && err !== null && (err as { name?: string }).name === "ZodError";
}

export function errorMiddleware(err: unknown, _req: express.Request, res: express.Response, _next: express.NextFunction) {
  if (err instanceof HttpError) {
    res.status(err.status).json({ error: { code: "COMMAND_REJECTED", message: err.message, details: err.details ?? {} } });
    return;
  }
  if (isDomainError(err)) {
    const status = err.code === "NOT_FOUND" ? 404 : 409;
    res.status(status).json({ error: { code: err.code, message: err.message, details: err.details ?? {} } });
    return;
  }
  if (isZodError(err)) {
    res.status(422).json({ error: { code: "INVALID_INPUT", message: "Invalid request", details: err.flatten?.() ?? {} } });
    return;
  }
  console.error(err);
  res.status(500).json({ error: { code: "INTERNAL", message: "Internal error" } });
}
