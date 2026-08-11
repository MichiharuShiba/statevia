import { describe, expect, it } from "vitest";
import {
  addWaitEventRow,
  connectWaitEventTarget,
  convertLegacyWaitToEvents,
  removeWaitEvent,
  renameWaitEventKey,
  setLegacyWaitEvent,
  setWaitEventTarget,
  setWaitEvents
} from "@/features/definition-editor/lib/setWaitEvents";
import type { DefinitionGraphDocument } from "@/features/definition-editor/lib/types";

function doc(nodes: DefinitionGraphDocument["nodes"]): DefinitionGraphDocument {
  return {
    version: 1,
    workflow: { name: "w" },
    nodes
  };
}

describe("setWaitEvents", () => {
  it("events 設定時に event / next / edges をクリアする", () => {
    // Arrange
    const before = doc([
      {
        name: "w1",
        type: "wait",
        event: "resume",
        next: "end",
        edges: [{ to: "other" }]
      },
      { name: "end", type: "end" }
    ]);

    // Act
    const after = setWaitEvents(before, "w1", { approve: "end", reject: "end" });

    // Assert
    const wait = after.nodes.find((node) => node.name === "w1");
    expect(wait).toEqual({
      name: "w1",
      type: "wait",
      events: { approve: "end", reject: "end" }
    });
  });

  it("Wait 以外のノードは変更しない", () => {
    // Arrange
    const before = doc([{ name: "a", type: "action", action: "noop" }]);

    // Act
    const after = setWaitEvents(before, "a", { x: "y" });

    // Assert
    expect(after).toBe(before);
  });
});

describe("setLegacyWaitEvent", () => {
  it("event のみ更新し next / edges を維持する", () => {
    // Arrange
    const before = doc([
      { name: "w1", type: "wait", event: "resume", next: "end", edges: [{ to: "end" }] }
    ]);

    // Act
    const after = setLegacyWaitEvent(before, "w1", "approve");

    // Assert
    expect(after.nodes[0]).toMatchObject({
      event: "approve",
      next: "end",
      edges: [{ to: "end" }]
    });
    expect(after.nodes[0].events).toBeUndefined();
  });
});

describe("convertLegacyWaitToEvents", () => {
  it("event と next を events マップへ変換する", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", event: "resume", next: "done" }]);

    // Act
    const after = convertLegacyWaitToEvents(before, "w1");

    // Assert
    expect(after.nodes[0]).toEqual({
      name: "w1",
      type: "wait",
      events: { resume: "done" }
    });
  });

  it("next が無いときは先頭 edge.to を遷移先にする", () => {
    // Arrange
    const before = doc([
      { name: "w1", type: "wait", event: "go", edges: [{ to: "a" }, { to: "b" }] }
    ]);

    // Act
    const after = convertLegacyWaitToEvents(before, "w1");

    // Assert
    expect(after.nodes[0]?.events).toEqual({ go: "a" });
  });
});

describe("connectWaitEventTarget", () => {
  it("空ターゲットの行を埋める", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { resume: "" } }]);

    // Act
    const after = connectWaitEventTarget(before, "w1", "end");

    // Assert
    expect(after.nodes[0]?.events).toEqual({ resume: "end" });
  });

  it("空きが無ければ一意キーを追加する", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { resume: "a" } }]);

    // Act
    const after = connectWaitEventTarget(before, "w1", "b");

    // Assert
    expect(after.nodes[0]?.events).toEqual({ resume: "a", event1: "b" });
  });

  it("Wait 以外は変更しない", () => {
    // Arrange
    const before = doc([{ name: "a", type: "action", action: "noop" }]);

    // Act
    const after = connectWaitEventTarget(before, "a", "end");

    // Assert
    expect(after).toBe(before);
  });
});

describe("removeWaitEvent / setWaitEventTarget", () => {
  it("イベント行の削除と遷移先更新ができる", () => {
    // Arrange
    const before = doc([
      { name: "w1", type: "wait", events: { approve: "ok", reject: "ng" } }
    ]);

    // Act
    const removed = removeWaitEvent(before, "w1", "reject");
    const retargeted = setWaitEventTarget(removed, "w1", "approve", "done");

    // Assert
    expect(removed.nodes[0]?.events).toEqual({ approve: "ok" });
    expect(retargeted.nodes[0]?.events).toEqual({ approve: "done" });
  });

  it("対象外ノードや未知イベントは変更しない", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { approve: "ok" } }]);

    // Act / Assert
    expect(removeWaitEvent(before, "missing", "approve")).toBe(before);
    expect(setWaitEventTarget(before, "w1", "unknown", "x")).toBe(before);
  });
});

describe("renameWaitEventKey / addWaitEventRow", () => {
  it("イベント名キーを変更できる", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { old: "a", keep: "b" } }]);

    // Act
    const after = renameWaitEventKey(before, "w1", "old", "new");

    // Assert
    expect(after.nodes[0]?.events).toEqual({ new: "a", keep: "b" });
  });

  it("同名へのリネームは no-op", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { resume: "a" } }]);

    // Act
    const after = renameWaitEventKey(before, "w1", "resume", "resume");

    // Assert
    expect(after).toBe(before);
  });

  it("空のイベント行を追加できる", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { resume: "a" } }]);

    // Act
    const after = addWaitEventRow(before, "w1");

    // Assert
    expect(after.nodes[0]?.events).toEqual({ resume: "a", event1: "" });
  });

  it("既存 event1 がある場合は event2 を採番する", () => {
    // Arrange
    const before = doc([{ name: "w1", type: "wait", events: { event1: "a" } }]);

    // Act
    const after = addWaitEventRow(before, "w1");

    // Assert
    expect(after.nodes[0]?.events).toEqual({ event1: "a", event2: "" });
  });
});
