/**
 * HTTP ミドルウェア
 */
import express from "express";
import { unprocessable } from "./errors.js";
import { Actor } from "../../domain/value-objects/actor.js";

export function actorFromReq(req: express.Request): Actor {
  const kind = (req.header("X-Actor-Kind") ?? "system") as any;
  const id = req.header("X-Actor-Id") ?? undefined;
  return { kind, id };
}

export function endpointKey(req: express.Request): string {
  // パスパラメータ（:id など）をパターンに正規化
  // 例: /executions/abc123/cancel -> /executions/:id/cancel
  let pattern = req.path;
  
  // Express の route パターンを使用（利用可能な場合）
  // または、パスパラメータを :id などのパターンに置換
  // 簡易実装: 実際のパスからパターンを推測
  // より正確には、Express の route 定義から取得するのが理想だが、
  // ここでは UUID や数値のようなパスパラメータを :id に正規化
  pattern = pattern.replace(/\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, '/:id');
  pattern = pattern.replace(/\/[0-9a-f]{32}/gi, '/:id'); // 32文字のhex（UUID形式なし）
  pattern = pattern.replace(/\/\d+/g, '/:id'); // 数値ID
  
  return `${req.method} ${req.baseUrl}${pattern}`;
}

export function requireIdempotencyKey(req: express.Request, res: express.Response, next: express.NextFunction) {
  const idem = req.header("X-Idempotency-Key");
  if (!idem) {
    throw unprocessable("Missing X-Idempotency-Key");
  }
  next();
}
