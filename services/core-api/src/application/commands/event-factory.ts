/**
 * イベントエンベロープ生成
 * コマンド・Orchestrator で共通利用
 */
import { EventEnvelope } from "../../domain/value-objects/event-envelope.js";
import { Actor } from "../../domain/value-objects/actor.js";

declare const crypto: { randomUUID(): string };

export function nowIso(): string {
  return new Date().toISOString();
}

export function mkEvent(
  executionId: string,
  type: string,
  actor: Actor,
  payload: Record<string, unknown>,
  correlationId?: string
): EventEnvelope {
  return {
    eventId: crypto.randomUUID(),
    executionId,
    type,
    occurredAt: nowIso(),
    actor,
    correlationId,
    schemaVersion: 1,
    payload
  };
}
