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
  it("maps EXECUTION_CANCELED to ExecutionStatusChanged", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_CANCELED", {}));
    expect(result).toEqual({
      type: "ExecutionStatusChanged",
      executionId: "ex-1",
      to: "CANCELED",
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

  it("ignores unsupported events", () => {
    const result = mapPersistedEventToStreamEvent(eventOf("EXECUTION_CREATED", { graphId: "hello" }));
    expect(result).toBeNull();
  });
});
