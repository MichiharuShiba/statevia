import { helloGraphDefinition } from "./definitions/hello.graph";
import type { GraphDefinition } from "./types";

const GRAPH_DEFINITIONS: Record<string, GraphDefinition> = {
  [helloGraphDefinition.graphId]: helloGraphDefinition
};

export function getGraphDefinition(graphId: string): GraphDefinition | null {
  return GRAPH_DEFINITIONS[graphId] ?? null;
}

