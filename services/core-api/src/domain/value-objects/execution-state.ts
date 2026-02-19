/**
 * ExecutionState 値オブジェクト
 * Execution と Node の集約状態を表す
 */
import { Execution } from "../entities/execution.js";
import { Node } from "../entities/node.js";

// 型を再エクスポート（他のモジュールから使いやすくするため）
export type { ExecutionStatus } from "../entities/execution.js";
export type { NodeStatus } from "../entities/node.js";

export type ExecutionState = Execution & {
  nodes: Record<string, Node>;
};
