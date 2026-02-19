/**
 * EventEnvelope 値オブジェクト
 * ドメインイベントのラッパー
 */
import { Actor } from "./actor.js";

export type EventEnvelope<TType extends string = string, TPayload = unknown> = {
  eventId: string; // uuid
  executionId: string;
  type: TType;
  occurredAt: string; // RFC3339
  actor: Actor;
  correlationId?: string;
  causationId?: string;
  schemaVersion: 1;
  payload: TPayload;
};
