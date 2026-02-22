export type GraphNodeDef = {
  nodeId: string;
  nodeType: string;
  label?: string;
  branch?: string;
};

export type GraphEdgeDef = {
  from: string;
  to: string;
  kind?: "normal" | "fork" | "join";
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

