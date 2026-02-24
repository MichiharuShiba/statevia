import { describe, expect, it } from "vitest";
import { mapPersistedEventToStreamEvent } from "../../../src/presentation/http/stream-events.js";
import type { PersistedEvent } from "../../../src/infrastructure/persistence/repositories/event-store.js";

function eventOf(type: string, payload: unknown): PersistedEvent {
  return {
    seq: 1,
    eventId: "evt-1",
    executionId: "ex-1",
    type,
    occurredAt: "2026-02-23T00:00:00.000Z",
    payload
  };
}

describe("mapPersistedEventToStreamEvent", () => {
  it("EXECUTION_STARTED を ExecutionStatusChanged にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_STARTED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "ACTIVE",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("EXECUTION_COMPLETED を ExecutionStatusChanged にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_COMPLETED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "COMPLETED",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("EXECUTION_FAILED を ExecutionStatusChanged にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_FAILED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "FAILED",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("EXECUTION_CANCELED を ExecutionStatusChanged にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_CANCELED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "CANCELED",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_FAILED を NodeFailed にマップする", () => {
    const result = mapPersistedEventToStreamEvent(
      eventOf("NODE_FAILED", {
        nodeId: "n-2",
        error: { message: "boom" }
      })
    );
    expect(result).toEqual({
      type: "NodeFailed",
      executionId: "ex-1",
      nodeId: "n-2",
      error: { message: "boom" },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_FAILED で error.message が文字列でないとき message を null にする", () => {
    const result = mapPersistedEventToStreamEvent(
      eventOf("NODE_FAILED", { nodeId: "n-2", error: {} })
    );
    expect(result).toEqual({
      type: "NodeFailed",
      executionId: "ex-1",
      nodeId: "n-2",
      error: { message: null },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_FAILED で nodeId が無いとき null を返す", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_FAILED", { error: { message: "x" } }));
    expect(result).toBeNull();
  });

  it("NODE_CANCELED を NodeCancelled にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CANCELED", { nodeId: "n-1", reason: "done" }));
    expect(result).toEqual({
      type: "NodeCancelled",
      executionId: "ex-1",
      nodeId: "n-1",
      cancel: { reason: "done" },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_CANCELED で nodeId が無いとき null を返す", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CANCELED", { reason: "x" }));
    expect(result).toBeNull();
  });

  it("NODE_CANCELED で reason が文字列でないとき reason を null にする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CANCELED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "NodeCancelled",
      executionId: "ex-1",
      nodeId: "n-1",
      cancel: { reason: null },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_CREATED を GraphUpdated にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CREATED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "IDLE" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_CREATED で nodeId が無いとき null を返す", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CREATED", {}));
    expect(result).toBeNull();
  });

  it("NODE_READY を GraphUpdated にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_READY", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "READY" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_STARTED を attempt 付き GraphUpdated にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_STARTED", { nodeId: "n-1", attempt: 2 }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: {
        nodes: [{ nodeId: "n-1", status: "RUNNING", attempt: 2 }]
      },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_STARTED で attempt が数でないときは attempt なしでマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_STARTED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: {
        nodes: [{ nodeId: "n-1", status: "RUNNING" }]
      },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_WAITING を waitKey 付き GraphUpdated にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_WAITING", { nodeId: "n-1", waitKey: "wk-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: {
        nodes: [{ nodeId: "n-1", status: "WAITING", waitKey: "wk-1" }]
      },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_WAITING で waitKey が文字列でないとき null にする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_WAITING", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: {
        nodes: [{ nodeId: "n-1", status: "WAITING", waitKey: null }]
      },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_RESUMED を GraphUpdated にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_RESUMED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "RUNNING" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("NODE_SUCCEEDED を GraphUpdated にマップする", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_SUCCEEDED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "SUCCEEDED" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("未対応イベントは無視する", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_CREATED", { graphId: "hello" }));
    expect(result).toBeNull();
  });

  it("ペイロードがオブジェクトでないときは null を返す", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_FAILED", null));
    expect(result).toBeNull();
  });
});
