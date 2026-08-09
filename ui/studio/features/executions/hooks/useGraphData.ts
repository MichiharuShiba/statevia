"use client";

import { useMemo } from "react";
import { resolveGroupBounds } from "../lib/grouping";
import { layoutGraph } from "@/shared/lib/graphLayout";
import { mergeGraph, type MergedGraphEdge, type MergedGraphNode } from "../lib/mergeGraph";
import type { ExecutionNodeDTO, ExecutionView } from "../types";
import type { GroupBounds } from "../lib/grouping";
import type { LayoutEdgeInput, PositionedNode } from "@/shared/lib/graphLayout";
import type { GraphDefinition } from "@/features/executions/graphs/types";

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
            const p = layoutMap[n.name];
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
 * リストはランタイム `nodeId`（UUID）、グラフは定義の `name`（状態キー）で選択するため、
 * `nodeName` およびマージ結果の `nodeName` でランタイム行へ寄せる。
 */
export function getNodeWithFallback(
  execution: ExecutionView | null,
  graphData: GraphData | null,
  nodeId: string | null
): ExecutionNodeDTO | null {
  if (!execution || !nodeId) return null;
  const key = nodeId.trim();

  const byRuntimeId = execution.nodes.find((n) => n.nodeId === key);
  if (byRuntimeId) return byRuntimeId;

  const byNodeNameKey = execution.nodes.find(
    (n) =>
      typeof n.nodeName === "string" &&
      n.nodeName.trim().length > 0 &&
      n.nodeName.trim().toLowerCase() === key.toLowerCase()
  );
  if (byNodeNameKey) return byNodeNameKey;

  const mergedNode = graphData?.mergedNodes.find((n) => n.name === key);
  if (!mergedNode) return null;

  const mergedState = mergedNode.nodeName.trim();
  if (mergedState.length > 0) {
    const byMergedNodeName = execution.nodes.find(
      (n) =>
        typeof n.nodeName === "string" &&
        n.nodeName.trim().toLowerCase() === mergedState.toLowerCase()
    );
    if (byMergedNodeName) return byMergedNodeName;
  }

  // 定義のみの IDLE 行など、実行 nodes に無いときはマージ結果から DTO を合成する。
  const fallbackFromMerged: ExecutionNodeDTO = {
    nodeId: mergedNode.nodeId,
    nodeName: mergedNode.nodeName,
    nodeType: mergedNode.nodeType,
    status: mergedNode.status,
    attempt: mergedNode.attempt,
    workerId: mergedNode.workerId,
    waitKey: mergedNode.waitKey,
    allowedEvents: mergedNode.allowedEvents ?? null,
    canceledByExecution: mergedNode.canceledByExecution
  };
  return fallbackFromMerged;
}
