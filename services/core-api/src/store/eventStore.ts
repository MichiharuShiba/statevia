import { PoolClient } from "pg";
import { EventEnvelope } from "../core/types.js";

export async function appendEventsTx(client: PoolClient, events: EventEnvelope[]) {
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