export type GraphNodeDef = {
  nodeId: string;
  nodeType: string;
  label?: string;
  branch?: string;
};

/** エッジの意味（視覚仕様: Next=実線, Resume=破線+イベント名, Cancel=太線+Cancel表示） */
export type EdgeType = "Next" | "Resume" | "Cancel";

export type GraphEdgeDef = {
  from: string;
  to: string;
  kind?: "normal" | "fork" | "join";
  /** エッジ種別（省略時は Next＝通常の遷移） */
  edgeType?: EdgeType;
  /** Resume 時: 表示するイベント名 */
  eventName?: string;
  /** Cancel 時: 表示する reason */
  cancelReason?: string;
  /** Cancel 時: 表示する cause（任意） */
  cancelCause?: string;
};

export type GraphGroupDef = {
  groupId: string;
  label: string;
  nodeIds: string[];
};

/**
 * 定義 YAML の `meta` と将来の API レスポンスを揃える。
 * 保存座標・dagre 向けヒント・グループ余白などを同一オブジェクトで扱う。
 */
export type GraphDefinitionMeta = {
  /** ノード ID → 保存済みキャンバス座標 */
  layout?: Record<string, { x: number; y: number }>;
  /** dagre rankdir: LR=左→右, TB=上→下, RL/BT も指定可 */
  direction?: "LR" | "TB" | "RL" | "BT";
  branchOrder?: string[];
  nodeSizeOverrides?: Record<string, { w: number; h: number }>;
  /**
   * 種別デフォルトより先に使う「通常ノードの幅・高さ」。
   * 定義エディタのコンパクト行高など（個別 override より優先度が低い）。
   */
  defaultNodeSize?: { w: number; h: number };
  groupPadding?: { x: number; y: number; header: number };
};

export type GraphDefinition = {
  graphId: string;
  nodes: GraphNodeDef[];
  edges: GraphEdgeDef[];
  groups?: GraphGroupDef[];
  meta?: GraphDefinitionMeta;
};
