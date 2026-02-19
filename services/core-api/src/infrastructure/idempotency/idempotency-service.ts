/**
 * IdempotencyService
 * 冪等性管理を担当
 */
import crypto from "node:crypto";
import { pool } from "../persistence/db.js";
import { conflict } from "../../presentation/http/errors.js";

export class IdempotencyService {
  static hashRequest(body: unknown): string {
    const json = JSON.stringify(body ?? null);
    return crypto.createHash("sha256").update(json).digest("hex");
  }

  static async load(
    idempotencyKey: string,
    endpoint: string
  ): Promise<null | { requestHash: string; responseStatus: number; responseBody: any }> {
    const r = await pool.query(
      `select request_hash, response_status, response_body
         from idempotency_keys
        where idempotency_key = $1 and endpoint = $2`,
      [idempotencyKey, endpoint]
    );
    if (r.rowCount === 0) return null;
    return {
      requestHash: r.rows[0].request_hash,
      responseStatus: r.rows[0].response_status,
      responseBody: r.rows[0].response_body
    };
  }

  static async save(
    idempotencyKey: string,
    endpoint: string,
    requestHash: string,
    responseStatus: number,
    responseBody: unknown
  ): Promise<void> {
    await pool.query(
      `insert into idempotency_keys(idempotency_key, endpoint, request_hash, response_status, response_body)
       values ($1,$2,$3,$4,$5)
       on conflict (idempotency_key, endpoint) do nothing`,
      [idempotencyKey, endpoint, requestHash, responseStatus, responseBody]
    );
  }

  static async enforceOrReplay(args: {
    idempotencyKey: string;
    endpoint: string;
    requestHash: string;
  }): Promise<null | { status: number; body: any }> {
    const existing = await this.load(args.idempotencyKey, args.endpoint);
    if (!existing) return null;

    if (existing.requestHash !== args.requestHash) {
      throw conflict("Idempotency key reused with different request body", {
        idempotencyKey: args.idempotencyKey,
        endpoint: args.endpoint
      });
    }
    return { status: existing.responseStatus, body: existing.responseBody };
  }
}
