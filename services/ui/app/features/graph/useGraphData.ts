"use client";

import { useMemo } from "react";
import { resolveGroupBounds } from "../../lib/grouping";
import { layoutGraph } from "../../lib/graphLayout";
import { mergeGraph, type MergedGraphEdge, type MergedGraphNode } from "../../lib/mergeGraph";
import type { ExecutionNodeDTO, ExecutionView } from "../../lib/types";
import type { GroupBounds } from "../../lib/grouping";
import type { LayoutEdgeInput, PositionedNode } from "../../lib/graphLayout";
import type { GraphDefinition } from "../../graphs/types";

/** GraphData の型定義。 */
export type GraphData = {
  graphId: string;
  definitionBased: boolean;
  mergedNodes: MergedGraphNode[];
  nodes: Array<PositionedNode<MergedGraphNode>>;
  edges: LayoutEdgeInput[];
  groups: GroupBounds[];
};

/** 実行ビューと定義グラフを合成したグラフデータを組み立てる。 */
export function useGraphData(
  execution: ExecutionView | null,
  graphDefinition: GraphDefinition | null
): GraphData | null {
  return useMemo(() => {
    if (!execution) return null;
    const merged = mergeGraph(execution, graphDefinition);
    const positioned = layoutGraph(
      merged.nodes,
      merged.edges.map((edge: MergedGraphEdge) => ({ ...edge })),
      merged.meta
    );
    const layoutMap = merged.meta?.layout;
    const nodes =
      layoutMap && Object.keys(layoutMap).length > 0
        ? positioned.nodes.map((n) => {
            const p = layoutMap[n.nodeId];
            return p ? { ...n, x: p.x, y: p.y } : n;
          })
        : positioned.nodes;
    const groups = resolveGroupBounds(nodes, positioned.edges, merged.groups, merged.meta);
    return {
      graphId: execution.graphId,
      definitionBased: merged.isDefinitionBased,
      mergedNodes: merged.nodes,
      nodes,
      edges: positioned.edges,
      groups
    };
  }, [execution, graphDefinition]);
}

/**
 * ノード詳細・Resume 用に `ExecutionNodeDTO` を解決する。
 * リストはランタイム `executionNodeId`（UUID）、グラフは定義の `nodeId`（状態キー）で選択するため、
 * `stateName` およびマージ結果の `stateName` でランタイム行へ寄せる。
 */
export function getNodeWithFallback(
  execution: ExecutionView | null,
  graphData: GraphData | null,
  nodeId: string | null
): ExecutionNodeDTO | null {
  if (!execution || !nodeId) return null;
  const key = nodeId.trim();

  const byRuntimeId = execution.nodes.find((n) => n.executionNodeId === key);
  if (byRuntimeId) return byRuntimeId;

  const byStateNameKey = execution.nodes.find(
    (n) =>
      typeof n.stateName === "string" &&
      n.stateName.trim().length > 0 &&
      n.stateName.trim().toLowerCase() === key.toLowerCase()
  );
  if (byStateNameKey) return byStateNameKey;

  const mergedNode = graphData?.mergedNodes.find((n) => n.nodeId === key);
  if (mergedNode) {
    const mergedState = mergedNode.stateName.trim();
    if (mergedState.length > 0) {
      const byMergedStateName = execution.nodes.find(
        (n) =>
          typeof n.stateName === "string" &&
          n.stateName.trim().toLowerCase() === mergedState.toLowerCase()
      );
      if (byMergedStateName) return byMergedStateName;
    }
  }

  if (!mergedNode) return null;

  return {
    executionNodeId: mergedNode.executionNodeId,
    stateName: mergedNode.stateName,
    nodeType: mergedNode.nodeType,
    status: mergedNode.status,
    attempt: mergedNode.attempt,
    workerId: mergedNode.workerId,
    waitKey: mergedNode.waitKey,
    canceledByExecution: mergedNode.canceledByExecution
  };
}
