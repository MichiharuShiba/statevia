/**
 * EventStore リポジトリ
 * イベントの永続化を担当
 */
import { PoolClient } from "pg";
import { EventEnvelope } from "../../../domain/value-objects/event-envelope.js";

export class EventStore {
  static async appendEvents(client: PoolClient, events: EventEnvelope[]): Promise<void> {
    for (const e of events) {
      await client.query(
        `insert into events(
          event_id, execution_id, type, occurred_at,
          actor_kind, actor_id, correlation_id, causation_id,
          schema_version, payload
        ) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)`,
        [
          e.eventId,
          e.executionId,
          e.type,
          e.occurredAt,
          e.actor.kind,
          e.actor.id ?? null,
          e.correlationId ?? null,
          e.causationId ?? null,
          e.schemaVersion,
          e.payload
        ]
      );
    }
  }
}
