/**
 * Definition Editor の YAML/Graph 共通ドキュメントモデル。
 * 永続化対象は既存スキーマ（version/workflow/nodes）のみを扱う。
 */
export const NODE_TYPES = ["start", "action", "wait", "fork", "join", "end"] as const;
export type NodeType = (typeof NODE_TYPES)[number];

export type EdgeCondition = {
  path: string;
  op: string;
  value: unknown;
};

export type DefinitionGraphEdge = {
  to: string;
  when?: EdgeCondition;
  order?: number;
  default?: boolean;
};

export type DefinitionGraphNode = {
  id: string;
  type: NodeType;
  action?: string;
  event?: string;
  next?: string;
  branches?: string[];
  edges?: DefinitionGraphEdge[];
  input?: Record<string, unknown>;
  mode?: "all";
};

/** エディタ専用メタ（実行エンジンは無視） */
export type DefinitionGraphMeta = {
  /** グラフキャンバス上のノード座標（node id → x/y） */
  layout?: Record<string, { x: number; y: number }>;
};

export type DefinitionGraphDocument = {
  version: number;
  workflow: {
    name: string;
  };
  nodes: DefinitionGraphNode[];
  meta?: DefinitionGraphMeta;
};

export type ParseDefinitionYamlResult = {
  document: DefinitionGraphDocument | null;
  diagnostics: string[];
};

export type ValidateGraphDocumentResult = {
  isValid: boolean;
  messages: string[];
};
