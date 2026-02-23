"use client";

import { useMemo } from "react";
import { resolveGroupBounds } from "../../lib/grouping";
import { layoutGraph } from "../../lib/graphLayout";
import { mergeGraph, type MergedGraphEdge, type MergedGraphNode } from "../../lib/mergeGraph";
import type { ExecutionDTO, ExecutionNodeDTO } from "../../lib/types";
import type { GroupBounds } from "../../lib/grouping";
import type { PositionedEdge, PositionedNode } from "../../lib/graphLayout";
import type { GraphDefinition } from "../../graphs/types";

export type GraphData = {
  graphId: string;
  definitionBased: boolean;
  mergedNodes: MergedGraphNode[];
  nodes: Array<PositionedNode<MergedGraphNode>>;
  edges: PositionedEdge[];
  groups: GroupBounds[];
};

export function useGraphData(
  execution: ExecutionDTO | null,
  graphDefinition: GraphDefinition | null
): GraphData | null {
  return useMemo(() => {
    if (!execution) return null;
    const merged = mergeGraph(execution, graphDefinition);
    const positioned = layoutGraph(
      merged.nodes,
      merged.edges.map((edge: MergedGraphEdge) => ({ ...edge })),
      merged.layoutHints
    );
    const groups = resolveGroupBounds(
      positioned.nodes,
      positioned.edges,
      merged.groups,
      merged.layoutHints
    );
    return {
      graphId: execution.graphId,
      definitionBased: merged.isDefinitionBased,
      mergedNodes: merged.nodes,
      nodes: positioned.nodes,
      edges: positioned.edges,
      groups
    };
  }, [execution, graphDefinition]);
}

export function getNodeWithFallback(
  execution: ExecutionDTO | null,
  graphData: GraphData | null,
  nodeId: string | null
): ExecutionNodeDTO | null {
  if (!execution || !nodeId) return null;
  const runtimeNode = execution.nodes.find((n) => n.nodeId === nodeId);
  if (runtimeNode) return runtimeNode;
  const mergedNode = graphData?.mergedNodes.find((n) => n.nodeId === nodeId);
  if (!mergedNode) return null;
  return {
    nodeId: mergedNode.nodeId,
    nodeType: mergedNode.nodeType,
    status: mergedNode.status,
    attempt: mergedNode.attempt,
    workerId: null,
    waitKey: mergedNode.waitKey,
    canceledByExecution: mergedNode.canceledByExecution
  };
}
