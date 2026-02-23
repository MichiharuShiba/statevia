/**
 * SSE 実行イベントストリームハンドラ
 * GET /executions/:executionId/stream のストリーミング処理を担当
 */
import type { Request, Response } from "express";
import { EventStore } from "../../infrastructure/persistence/repositories/event-store.js";
import { mapPersistedEventToStreamEvent, type StreamEvent } from "./stream-events.js";

const POLL_INTERVAL_MS = 1000;
const HEARTBEAT_INTERVAL_MS = 15000;
const POLL_BATCH_SIZE = 200;

export async function streamExecutionEvents(
  executionId: string,
  res: Response,
  req: Request
): Promise<void> {
  let closed = false;
  let polling = false;
  let lastSeq = await EventStore.loadLatestSeq(executionId);

  res.status(200);
  res.setHeader("Content-Type", "text/event-stream; charset=utf-8");
  res.setHeader("Cache-Control", "no-cache, no-transform");
  res.setHeader("Connection", "keep-alive");
  res.flushHeaders();

  const writeEvent = (event: StreamEvent) => {
    res.write(`event: ${event.type}\n`);
    res.write(`data: ${JSON.stringify(event)}\n\n`);
  };

  const poll = async () => {
    if (closed || polling) return;
    polling = true;
    try {
      const events = await EventStore.listSince(executionId, lastSeq, POLL_BATCH_SIZE);
      for (const event of events) {
        lastSeq = event.seq;
        const streamEvent = mapPersistedEventToStreamEvent(event);
        if (!streamEvent) continue;
        writeEvent(streamEvent);
      }
    } catch (error) {
      if (!closed) {
        res.write(`event: error\n`);
        res.write(`data: ${JSON.stringify({ message: "stream polling failed" })}\n\n`);
      }
      console.error(`SSE polling failed for execution ${executionId}:`, error);
    } finally {
      polling = false;
    }
  };

  const heartbeatTimer = setInterval(() => {
    if (!closed) {
      res.write(": ping\n\n");
    }
  }, HEARTBEAT_INTERVAL_MS);

  const pollTimer = setInterval(() => {
    void poll();
  }, POLL_INTERVAL_MS);

  void poll();

  req.on("close", () => {
    closed = true;
    clearInterval(heartbeatTimer);
    clearInterval(pollTimer);
  });
}
