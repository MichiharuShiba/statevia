import { describe, expect, it } from "vitest";
import { validateGraphDocument, type ValidateGraphDocumentMessageOptions } from "@/features/definition-editor/lib/validateGraphDocument";
import type { DefinitionGraphDocument } from "@/features/definition-editor/lib/types";

function opts(): ValidateGraphDocumentMessageOptions {
  const m = (prefix: string) => (nodeName: string) => `${prefix}:${nodeName}`;
  const m2 = (prefix: string) => (a: string, b: string) => `${prefix}:${a}:${b}`;
  return {
    nodesRequired: () => "nodesRequired",
    nodeNameRequired: () => "nodeNameRequired",
    duplicateNodeName: m("dup"),
    startCountInvalid: (c) => `startCount:${c}`,
    endCountInvalid: (c) => `endCount:${c}`,
    startRequiresTransition: m("startReq"),
    actionRequired: m("actionReq"),
    actionRequiresTransition: m("actionTrans"),
    waitEventRequired: m("waitEvt"),
    waitRequiresTransition: m("waitTrans"),
    waitEventsAndEventTogether: m("waitBoth"),
    waitEventsCannotHaveEdges: m("waitEdges"),
    waitEventTargetRequired: (nodeName, eventName) => `waitTarget:${nodeName}:${eventName}`,
    forkBranchesRequired: m("fork"),
    joinRequiresTransition: m("joinTrans"),
    joinModeInvalid: m("joinMode"),
    endCannotHaveTransition: m("endTrans"),
    edgeToRequired: m("edgeTo"),
    edgeWhenPathRequired: m("whenPath"),
    edgeWhenOpRequired: m("whenOp"),
    edgeWhenValueRequired: m("whenValueReq"),
    edgeWhenValueInInvalid: m("whenIn"),
    edgeWhenValueBetweenInvalid: m("whenBetween"),
    edgeDefaultMultiple: m("defaultMulti"),
    selfReferenceEdge: m("selfRef"),
    missingTargetNode: m2("missing")
  };
}

function linearActionWithEdge(when: { path: string; op: string; value?: unknown }): DefinitionGraphDocument {
  return {
    version: 1,
    workflow: { name: "w" },
    nodes: [
      { name: "s", type: "start", next: "a" },
      {
        name: "a",
        type: "action",
        action: "noop",
        edges: [{ to: "e", when }]
      },
      { name: "e", type: "end" }
    ]
  };
}

describe("validateGraphDocument / edge.when.value", () => {
  it("EQ で value が空なら edgeWhenValueRequired", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "EQ", value: "" }),
      opts()
    );
    expect(r.isValid).toBe(false);
    expect(r.messages.some((x) => x.startsWith("whenValueReq:"))).toBe(true);
  });

  it("EQ で value が 0 なら有効", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "EQ", value: 0 }),
      opts()
    );
    expect(r.isValid).toBe(true);
  });

  it("EXISTS で value が空でも有効", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "EXISTS", value: "" }),
      opts()
    );
    expect(r.messages.filter((x) => x.startsWith("whenValueReq:"))).toHaveLength(0);
    expect(r.isValid).toBe(true);
  });

  it("IN で空配列なら whenIn", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "IN", value: [] }),
      opts()
    );
    expect(r.isValid).toBe(false);
    expect(r.messages.some((x) => x.startsWith("whenIn:"))).toBe(true);
  });

  it("IN で非空配列なら有効", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "IN", value: ["a"] }),
      opts()
    );
    expect(r.isValid).toBe(true);
  });

  it("BETWEEN で要素1件なら whenBetween", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "BETWEEN", value: [1] }),
      opts()
    );
    expect(r.isValid).toBe(false);
    expect(r.messages.some((x) => x.startsWith("whenBetween:"))).toBe(true);
  });

  it("BETWEEN で要素2件なら有効", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "BETWEEN", value: [1, 10] }),
      opts()
    );
    expect(r.isValid).toBe(true);
  });

  it("IN で JSON 配列文字列なら有効", () => {
    const r = validateGraphDocument(
      linearActionWithEdge({ path: "$.x", op: "IN", value: '["x","y"]' }),
      opts()
    );
    expect(r.isValid).toBe(true);
  });

  it("default=true が同一ノードで2件以上なら defaultMulti", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "a" },
        {
          name: "a",
          type: "action",
          action: "noop",
          edges: [
            { to: "e", default: true },
            { to: "e", default: true }
          ]
        },
        { name: "e", type: "end" }
      ]
    };
    const r = validateGraphDocument(doc, opts());
    expect(r.isValid).toBe(false);
    expect(r.messages.some((x) => x.startsWith("defaultMulti:"))).toBe(true);
  });

  it("join が mode なし・next のみなら有効", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "j" },
        { name: "j", type: "join", next: "e" },
        { name: "e", type: "end" }
      ]
    };
    expect(validateGraphDocument(doc, opts()).isValid).toBe(true);
  });

  it("action.error が未定義ノードを指すと missing", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "a" },
        { name: "a", type: "action", action: "noop", next: "e", error: "unknown" },
        { name: "e", type: "end" }
      ]
    };
    const r = validateGraphDocument(doc, opts());
    expect(r.isValid).toBe(false);
    expect(r.messages).toContain("missing:a:unknown");
  });

  it("action.error が自己参照のとき selfRef", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "a" },
        { name: "a", type: "action", action: "noop", next: "e", error: "a" },
        { name: "e", type: "end" }
      ]
    };
    const r = validateGraphDocument(doc, opts());
    expect(r.isValid).toBe(false);
    expect(r.messages).toContain("selfRef:a");
  });
});

describe("validateGraphDocument / wait.events", () => {
  it("events マップがあれば next/edges なしでも有効", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        {
          name: "w1",
          type: "wait",
          events: { approve: "ok", reject: "ng" }
        },
        { name: "ok", type: "action", action: "noop", next: "e" },
        { name: "ng", type: "action", action: "noop", next: "e" },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(true);
  });

  it("旧形式 event+next は引き続き有効", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        { name: "w1", type: "wait", event: "resume", next: "e" },
        { name: "e", type: "end" }
      ]
    };
    expect(validateGraphDocument(doc, opts()).isValid).toBe(true);
  });

  it("events と event の併用は拒否", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        {
          name: "w1",
          type: "wait",
          event: "resume",
          events: { approve: "e" }
        },
        { name: "e", type: "end" }
      ]
    };
    const r = validateGraphDocument(doc, opts());
    expect(r.isValid).toBe(false);
    expect(r.messages.some((x) => x.startsWith("waitBoth:"))).toBe(true);
  });

  it("events と edges の併用は拒否", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        {
          name: "w1",
          type: "wait",
          events: { approve: "e" },
          edges: [{ to: "e" }]
        },
        { name: "e", type: "end" }
      ]
    };
    const r = validateGraphDocument(doc, opts());
    expect(r.isValid).toBe(false);
    expect(r.messages.some((x) => x.startsWith("waitEdges:"))).toBe(true);
  });

  it("events の遷移先が空なら waitTarget", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        { name: "w1", type: "wait", events: { approve: "  " } },
        { name: "e", type: "end" }
      ]
    };
    const r = validateGraphDocument(doc, opts());
    expect(r.isValid).toBe(false);
    expect(r.messages).toContain("waitTarget:w1:approve");
  });

  it("events の遷移先が未定義ノードなら missing", () => {
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "w" },
      nodes: [
        { name: "s", type: "start", next: "w1" },
        { name: "w1", type: "wait", events: { approve: "unknown" } },
        { name: "e", type: "end" }
      ]
    };
    const r = validateGraphDocument(doc, opts());
    expect(r.isValid).toBe(false);
    expect(r.messages).toContain("missing:w1:unknown");
  });
});
