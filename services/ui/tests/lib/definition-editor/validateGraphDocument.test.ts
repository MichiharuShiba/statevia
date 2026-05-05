import { describe, expect, it } from "vitest";
import { validateGraphDocument, type ValidateGraphDocumentMessageOptions } from "../../../app/lib/definition-editor/validateGraphDocument";
import type { DefinitionGraphDocument } from "../../../app/lib/definition-editor/types";

function opts(): ValidateGraphDocumentMessageOptions {
  const m = (prefix: string) => (nodeId: string) => `${prefix}:${nodeId}`;
  const m2 = (prefix: string) => (a: string, b: string) => `${prefix}:${a}:${b}`;
  return {
    nodesRequired: () => "nodesRequired",
    nodeIdRequired: () => "nodeIdRequired",
    duplicateNodeId: m("dup"),
    startCountInvalid: (c) => `startCount:${c}`,
    endCountInvalid: (c) => `endCount:${c}`,
    startRequiresTransition: m("startReq"),
    actionRequired: m("actionReq"),
    actionRequiresTransition: m("actionTrans"),
    waitEventRequired: m("waitEvt"),
    waitRequiresTransition: m("waitTrans"),
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
      { id: "s", type: "start", next: "a" },
      {
        id: "a",
        type: "action",
        action: "noop",
        edges: [{ to: "e", when }]
      },
      { id: "e", type: "end" }
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
        { id: "s", type: "start", next: "a" },
        {
          id: "a",
          type: "action",
          action: "noop",
          edges: [
            { to: "e", default: true },
            { to: "e", default: true }
          ]
        },
        { id: "e", type: "end" }
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
        { id: "s", type: "start", next: "j" },
        { id: "j", type: "join", next: "e" },
        { id: "e", type: "end" }
      ]
    };
    expect(validateGraphDocument(doc, opts()).isValid).toBe(true);
  });
});
