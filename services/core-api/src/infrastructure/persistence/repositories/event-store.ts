/**
 * EventStore リポジトリ
 * イベントの永続化を担当
 */
import { PoolClient } from "pg";
import { EventEnvelope } from "../../../domain/value-objects/event-envelope.js";
import { pool } from "../db.js";

export type PersistedEvent = {
  seq: number;
  eventId: string;
  executionId: string;
  type: string;
  occurredAt: string;
  payload: unknown;
};

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

  static async loadLatestSeq(executionId: string): Promise<number> {
    const result = await pool.query<{ seq: number | null }>(
      `select max(seq)::bigint as seq
         from events
        where execution_id = $1`,
      [executionId]
    );
    return Number(result.rows[0]?.seq ?? 0);
  }

  static async listSince(executionId: string, afterSeq: number, limit = 200): Promise<PersistedEvent[]> {
    const result = await pool.query<{
      seq: string;
      event_id: string;
      execution_id: string;
      type: string;
      occurred_at: string;
      payload: unknown;
    }>(
      `select seq, event_id, execution_id, type, occurred_at, payload
         from events
        where execution_id = $1
          and seq > $2
        order by seq asc
        limit $3`,
      [executionId, afterSeq, limit]
    );

    return result.rows.map((row) => ({
      seq: Number(row.seq),
      eventId: row.event_id,
      executionId: row.execution_id,
      type: row.type,
      occurredAt: row.occurred_at,
      payload: row.payload
    }));
  }
}
