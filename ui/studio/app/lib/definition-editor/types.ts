/**
 * Definition Editor の YAML/Graph 共通ドキュメントモデル。
 * 永続化対象は既存スキーマ（version/workflow/nodes）のみを扱う。
 */
export const NODE_TYPES = ["start", "action", "wait", "fork", "join", "end"] as const;
/** 定義グラフノードの種別。 */
export type NodeType = (typeof NODE_TYPES)[number];

/** 定義グラフ辺の条件式。 */
export type EdgeCondition = {
  path: string;
  op: string;
  /** EXISTS 等では省略されうる */
  value?: unknown;
};

/** 定義グラフドキュメント内の辺。 */
export type DefinitionGraphEdge = {
  to: string;
  when?: EdgeCondition;
  order?: number;
  default?: boolean;
};

/** 定義グラフドキュメント内のノード。 */
export type DefinitionGraphNode = {
  id: string;
  type: NodeType;
  action?: string;
  /** action: 失敗時の遷移先（on.Failed.next） */
  error?: string;
  event?: string;
  next?: string;
  branches?: string[];
  edges?: DefinitionGraphEdge[];
  /** action: パス文字列・リテラル、またはキー→パス／リテラルのマップ（ローダー ParseStrictInputMapping と整合） */
  input?: string | Record<string, unknown>;
  /** join: conversion spec では省略可。指定時は `all` のみ */
  mode?: "all";
};

/** エディタ専用メタ（実行エンジンは無視） */
export type DefinitionGraphMeta = {
  /** グラフキャンバス上のノード座標（node id → x/y） */
  layout?: Record<string, { x: number; y: number }>;
};

/** 定義エディタが編集するグラフドキュメント。 */
export type DefinitionGraphDocument = {
  version: number;
  workflow: {
    name: string;
    /** YAML の workflow.id（名前未指定時ローダーが名前解決に利用） */
    id?: string;
    description?: string;
  };
  nodes: DefinitionGraphNode[];
  meta?: DefinitionGraphMeta;
};

/** YAML パース結果。 */
export type ParseDefinitionYamlResult = {
  document: DefinitionGraphDocument | null;
  diagnostics: string[];
};

/** グラフドキュメント検証結果。 */
export type ValidateGraphDocumentResult = {
  isValid: boolean;
  messages: string[];
};
