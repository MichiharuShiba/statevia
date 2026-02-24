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

export type LayoutHints = {
  direction?: "LR";
  branchOrder?: string[];
  nodeSizeOverrides?: Record<string, { w: number; h: number }>;
  groupPadding?: { x: number; y: number; header: number };
};

export type GraphDefinition = {
  graphId: string;
  nodes: GraphNodeDef[];
  edges: GraphEdgeDef[];
  groups?: GraphGroupDef[];
  layoutHints?: LayoutHints;
};

