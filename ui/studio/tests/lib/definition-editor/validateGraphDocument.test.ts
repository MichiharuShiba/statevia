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
    missingTargetNode: m2("missing"),
    forkRegionIngressFromOutside: m2("ingress"),
    forkRegionEgressWithoutJoin: (fromName, toName, joinName) => `egress:${fromName}:${toName}:${joinName}`,
    forkRegionWaitTargetOutside: m2("waitOutside")
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

describe("validateGraphDocument / fork region light", () => {
  it("標準 Fork-Join は有効", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "simple" },
      nodes: [
        { name: "s", type: "start", next: "fork1" },
        { name: "fork1", type: "fork", branches: ["left", "right"] },
        { name: "left", type: "action", action: "noop", next: "join1" },
        { name: "right", type: "action", action: "noop", next: "join1" },
        { name: "join1", type: "join", next: "e" },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(true);
  });

  it("枝から Join を経由せず end へ出ると egress", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "A2" },
      nodes: [
        { name: "s", type: "start", next: "fork1" },
        { name: "fork1", type: "fork", branches: ["left", "right"] },
        { name: "left", type: "action", action: "noop", next: "e" },
        { name: "right", type: "action", action: "noop", next: "join1" },
        { name: "join1", type: "join", next: "e" },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(false);
    expect(r.messages).toContain("egress:left:e:join1");
  });

  it("Join 後から枝先頭へ戻ると ingress", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "A1" },
      nodes: [
        { name: "s", type: "start", next: "fork1" },
        { name: "fork1", type: "fork", branches: ["left", "right"] },
        { name: "left", type: "action", action: "noop", next: "join1" },
        { name: "right", type: "action", action: "noop", next: "join1" },
        { name: "join1", type: "join", next: "decide" },
        {
          name: "decide",
          type: "wait",
          events: { Finish: "e", Rogue: "left" }
        },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(false);
    expect(r.messages).toContain("ingress:decide:left");
  });

  it("枝内 Wait の一方が領域外なら waitOutside", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "D2" },
      nodes: [
        { name: "s", type: "start", next: "fork1" },
        { name: "fork1", type: "fork", branches: ["left", "wait2"] },
        { name: "left", type: "action", action: "noop", next: "join1" },
        {
          name: "wait2",
          type: "wait",
          events: { Resume: "join1", Timeout: "e" }
        },
        { name: "join1", type: "join", next: "e" },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(false);
    expect(r.messages).toContain("waitOutside:wait2:e");
    expect(r.messages).toContain("egress:wait2:e:join1");
  });

  it("Join 後に同一 Fork へ戻る循環は有効", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "cyclic" },
      nodes: [
        { name: "s", type: "start", next: "fork1" },
        { name: "fork1", type: "fork", branches: ["a", "b"] },
        { name: "a", type: "action", action: "noop", next: "join1" },
        { name: "b", type: "action", action: "noop", next: "join1" },
        { name: "join1", type: "join", next: "decide" },
        {
          name: "decide",
          type: "wait",
          events: { Again: "fork1", Finish: "e" }
        },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(true);
  });

  it("ネスト Fork-Join は有効", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "nested" },
      nodes: [
        { name: "s", type: "start", next: "outerFork" },
        { name: "outerFork", type: "fork", branches: ["outerFast", "innerFork"] },
        { name: "outerFast", type: "action", action: "noop", next: "outerJoin" },
        { name: "innerFork", type: "fork", branches: ["innerA", "innerB"] },
        { name: "innerA", type: "action", action: "noop", next: "innerJoin" },
        { name: "innerB", type: "action", action: "noop", next: "innerJoin" },
        { name: "innerJoin", type: "join", next: "outerJoin" },
        { name: "outerJoin", type: "join", next: "e" },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(true);
  });

  it("枝先頭 Wait が events で Join を供給する定義は有効", () => {
    // Arrange
    const doc: DefinitionGraphDocument = {
      version: 1,
      workflow: { name: "E1" },
      nodes: [
        { name: "s", type: "start", next: "fork1" },
        { name: "fork1", type: "fork", branches: ["left", "wait2"] },
        { name: "left", type: "action", action: "noop", next: "join1" },
        { name: "wait2", type: "wait", events: { Resume: "right" } },
        { name: "right", type: "action", action: "noop", next: "join1" },
        { name: "join1", type: "join", next: "e" },
        { name: "e", type: "end" }
      ]
    };

    // Act
    const r = validateGraphDocument(doc, opts());

    // Assert
    expect(r.isValid).toBe(true);
  });
});
