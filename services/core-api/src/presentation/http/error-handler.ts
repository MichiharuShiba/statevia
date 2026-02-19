/**
 * HTTP エラーハンドラー
 */
import express from "express";
import { HttpError } from "./errors.js";

export function errorMiddleware(err: any, _req: express.Request, res: express.Response, _next: express.NextFunction) {
  if (err instanceof HttpError) {
    res.status(err.status).json({ error: { code: "COMMAND_REJECTED", message: err.message, details: err.details ?? {} } });
    return;
  }
  // DomainError の処理
  if (err?.name === "DomainError") {
    res.status(409).json({ error: { code: err.code, message: err.message, details: err.details ?? {} } });
    return;
  }
  // zod
  if (err?.name === "ZodError") {
    res.status(422).json({ error: { code: "INVALID_INPUT", message: "Invalid request", details: err.flatten?.() ?? {} } });
    return;
  }
  console.error(err);
  res.status(500).json({ error: { code: "INTERNAL", message: "Internal error" } });
}
