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
  it("maps EXECUTION_STARTED to ExecutionStatusChanged", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_STARTED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "ACTIVE",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("maps EXECUTION_COMPLETED to ExecutionStatusChanged", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_COMPLETED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "COMPLETED",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("maps EXECUTION_FAILED to ExecutionStatusChanged", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_FAILED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "FAILED",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("maps EXECUTION_CANCELED to ExecutionStatusChanged", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_CANCELED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "CANCELED",
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("maps NODE_FAILED to NodeFailed", () => {
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

  it("maps NODE_FAILED to NodeFailed with null message when error.message is not string", () => {
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

  it("returns null for NODE_FAILED when nodeId is missing", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_FAILED", { error: { message: "x" } }));
    expect(result).toBeNull();
  });

  it("maps NODE_CANCELED to NodeCancelled", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CANCELED", { nodeId: "n-1", reason: "done" }));
    expect(result).toEqual({
      type: "NodeCancelled",
      executionId: "ex-1",
      nodeId: "n-1",
      cancel: { reason: "done" },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("returns null for NODE_CANCELED when nodeId is missing", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CANCELED", { reason: "x" }));
    expect(result).toBeNull();
  });

  it("maps NODE_CANCELED to NodeCancelled with null reason when reason is not string", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CANCELED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "NodeCancelled",
      executionId: "ex-1",
      nodeId: "n-1",
      cancel: { reason: null },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("maps NODE_CREATED to GraphUpdated", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CREATED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "IDLE" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("returns null for NODE_CREATED when nodeId is missing", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_CREATED", {}));
    expect(result).toBeNull();
  });

  it("maps NODE_READY to GraphUpdated", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_READY", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "READY" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("maps NODE_STARTED to GraphUpdated with attempt", () => {
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

  it("maps NODE_STARTED to GraphUpdated without attempt when not number", () => {
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

  it("maps NODE_WAITING to GraphUpdated with waitKey", () => {
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

  it("maps NODE_WAITING to GraphUpdated with null waitKey when not string", () => {
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

  it("maps NODE_RESUMED to GraphUpdated", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_RESUMED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "RUNNING" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("maps NODE_SUCCEEDED to GraphUpdated", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_SUCCEEDED", { nodeId: "n-1" }));
    expect(result).toEqual({
      type: "GraphUpdated",
      executionId: "ex-1",
      patch: { nodes: [{ nodeId: "n-1", status: "SUCCEEDED" }] },
      at: "2026-02-23T00:00:00.000Z"
    });
  });

  it("ignores unsupported events", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_CREATED", { graphId: "hello" }));
    expect(result).toBeNull();
  });

  it("handles non-object payload as empty object", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("NODE_FAILED", null));
    expect(result).toBeNull();
  });
});
