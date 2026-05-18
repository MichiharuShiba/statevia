import { graph1GraphDefinition } from "./definitions/graph-1.graph";
import { helloGraphDefinition } from "./definitions/hello.graph";
import { nodesFormatShowcaseGraphDefinition } from "./definitions/nodes-format-showcase.graph";
import type { GraphDefinition } from "./types";

const GRAPH_DEFINITIONS: Record<string, GraphDefinition> = {
  [helloGraphDefinition.graphId]: helloGraphDefinition,
  [graph1GraphDefinition.graphId]: graph1GraphDefinition,
  [nodesFormatShowcaseGraphDefinition.graphId]: nodesFormatShowcaseGraphDefinition
};

/** graphId から静的定義グラフを取得する。 */
export function getGraphDefinition(graphId: string): GraphDefinition | null {
  return GRAPH_DEFINITIONS[graphId] ?? null;
}

