import type { GraphDefinition } from "../types";

/**
 * `nodes` / `edges` / `groups` / `meta` 形式のうち、現行 UI（mergeGraph・layoutGraph・NodeGraphView）で解釈できるパターンを1本に集約したサンプル。
 *
 * **ノード (`GraphNodeDef`)**
 * - `nodeId` … 必須。エッジ・`meta.layout`・グループのキー。
 * - `nodeType` … 必須。レイアウトの並び・サイズヒントに利用（Start/Task/Fork/Join/Wait/Success 等）。
 * - `label` … 任意。
 * - `branch` … 任意。並列ブランチの横オフセット（`meta.branchOrder` と併用）。
 * - `stateName` … 任意。省略時は実行マージで `nodeId` とみなす。実行グラフの `stateName` と突き合わせるときに使用。
 *
 * **エッジ (`GraphEdgeDef`)**
 * - `from` / `to` … 必須（ノードの `nodeId`）。
 * - `kind` … 任意。`normal` | `fork` | `join`（視覚・フォーク／ジョイン用途）。
 * - `edgeType` … 任意。省略時は Next 相当。`Resume`（`eventName` とセット）、`Cancel`（`cancelReason` / `cancelCause` とセット）。
 *
 * **グループ (`GraphGroupDef`) … 任意**
 *
 * **メタ (`GraphDefinitionMeta`) … 任意**
 * - `direction` … dagre rankdir（TB/LR/RL/BT）。
 * - `branchOrder` … `branch` 付きノードの並び。
 * - `layout` … ノード ID → 保存座標（あれば UI が優先）。
 * - `defaultNodeSize` / `nodeSizeOverrides` … レイアウト時のノード矩形。
 * - `groupPadding` … グループ枠の余白。
 */
export const nodesFormatShowcaseGraphDefinition: GraphDefinition = {
  graphId: "nodes-format-showcase",
  nodes: [
    { nodeId: "start", nodeType: "Start", label: "Start（label あり）" },
    { nodeId: "task-a", nodeType: "Task", label: "Task（通常）" },
    { nodeId: "fork-1", nodeType: "Fork", label: "Fork" },
    { nodeId: "task-b", nodeType: "Task", label: "Branch B", branch: "b" },
    { nodeId: "task-c", nodeType: "Wait", label: "Branch C（Wait・branch）", branch: "c" },
    { nodeId: "join-1", nodeType: "Join", label: "Join" },
    { nodeId: "after-join", nodeType: "Task", label: "合流後" },
    {
      nodeId: "canvas-1",
      stateName: "state-from-engine",
      nodeType: "Task",
      label: "nodeId≠stateName"
    },
    { nodeId: "pre-cancel", nodeType: "Task", label: "取消分岐手前" },
    { nodeId: "end-success", nodeType: "Success", label: "成功終端" },
    { nodeId: "end-cancelled", nodeType: "Success", label: "取消終端（Cancel エッジ）" }
  ],
  edges: [
    { from: "start", to: "task-a" },
    { from: "task-a", to: "fork-1" },
    { from: "fork-1", to: "task-b", kind: "fork" },
    { from: "fork-1", to: "task-c", kind: "fork" },
    { from: "task-b", to: "join-1", kind: "join" },
    {
      from: "task-c",
      to: "join-1",
      kind: "join",
      edgeType: "Resume",
      eventName: "ResumeEvt"
    },
    { from: "join-1", to: "after-join" },
    { from: "after-join", to: "canvas-1" },
    { from: "canvas-1", to: "pre-cancel" },
    { from: "pre-cancel", to: "end-success" },
    {
      from: "pre-cancel",
      to: "end-cancelled",
      edgeType: "Cancel",
      cancelReason: "user_abort",
      cancelCause: "demo"
    }
  ],
  groups: [
    {
      groupId: "parallel-block",
      label: "Fork / Join グループ",
      nodeIds: ["fork-1", "task-b", "task-c", "join-1"]
    }
  ],
  meta: {
    direction: "TB",
    branchOrder: ["b", "c"],
    groupPadding: { x: 40, y: 30, header: 28 },
    defaultNodeSize: { w: 240, h: 72 },
    nodeSizeOverrides: {
      "canvas-1": { w: 260, h: 120 }
    },
    layout: {
      start: { x: 400, y: 40 },
      "task-a": { x: 400, y: 160 },
      "fork-1": { x: 400, y: 280 }
    }
  }
};
