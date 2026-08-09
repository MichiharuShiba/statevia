import type { GraphDefinition } from "../types";

/**
 * `nodes` / `edges` / `groups` / `meta` 形式のうち、現行 UI（mergeGraph・layoutGraph・NodeGraphView）で解釈できるパターンを1本に集約したサンプル。
 *
 * **ノード (`GraphNodeDef`)**
 * - `nodeName` … 必須。定義グラフ上で一意なノード名。エッジ・`meta.layout`・グループのキー（実行時の StateName と一致）。
 * - `nodeType` … 必須。レイアウトの並び・サイズヒントに利用（Start/Task/Fork/Join/Wait/Success 等）。
 * - `label` … 任意。
 * - `branch` … 任意。並列ブランチの横オフセット（`meta.branchOrder` と併用）。
 *
 * **エッジ (`GraphEdgeDef`)**
 * - `from` / `to` … 必須（ノードの `nodeName`）。
 * - `kind` … 任意。`normal` | `fork` | `join`（視覚・フォーク／ジョイン用途）。
 * - `edgeType` … 任意。省略時は Next 相当。`Resume`（`eventName` とセット）、`Cancel`（`cancelReason` / `cancelCause` とセット）。
 *
 * **グループ (`GraphGroupDef`) … 任意**
 *
 * **メタ (`GraphDefinitionMeta`) … 任意**
 * - `direction` … dagre rankdir（TB/LR/RL/BT）。
 * - `branchOrder` … `branch` 付きノードの並び。
 * - `layout` … ノード名 → 保存座標（あれば UI が優先）。
 * - `defaultNodeSize` / `nodeSizeOverrides` … レイアウト時のノード矩形。
 * - `groupPadding` … グループ枠の余白。
 */
export const nodesFormatShowcaseGraphDefinition: GraphDefinition = {
  graphId: "nodes-format-showcase",
  nodes: [
    { nodeName: "start", nodeType: "Start", label: "Start（label あり）" },
    { nodeName: "task-a", nodeType: "Task", label: "Task（通常）" },
    { nodeName: "fork-1", nodeType: "Fork", label: "Fork" },
    { nodeName: "task-b", nodeType: "Task", label: "Branch B", branch: "b" },
    { nodeName: "task-c", nodeType: "Wait", label: "Branch C（Wait・branch）", branch: "c" },
    { nodeName: "join-1", nodeType: "Join", label: "Join" },
    { nodeName: "after-join", nodeType: "Task", label: "合流後" },
    { nodeName: "state-from-engine", nodeType: "Task", label: "実行状態名の例" },
    { nodeName: "pre-cancel", nodeType: "Task", label: "取消分岐手前" },
    { nodeName: "end-success", nodeType: "Success", label: "成功終端" },
    { nodeName: "end-cancelled", nodeType: "Success", label: "取消終端（Cancel エッジ）" }
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
    { from: "after-join", to: "state-from-engine" },
    { from: "state-from-engine", to: "pre-cancel" },
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
      nodeNames: ["fork-1", "task-b", "task-c", "join-1"]
    }
  ],
  meta: {
    direction: "TB",
    branchOrder: ["b", "c"],
    groupPadding: { x: 40, y: 30, header: 28 },
    defaultNodeSize: { w: 240, h: 72 },
    nodeSizeOverrides: {
      "state-from-engine": { w: 260, h: 120 }
    },
    layout: {
      start: { x: 400, y: 40 },
      "task-a": { x: 400, y: 160 },
      "fork-1": { x: 400, y: 280 }
    }
  }
};
