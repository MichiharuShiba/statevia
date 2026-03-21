"use client";

import { useEffect, useState } from "react";
import { getGraphDefinition } from "../../graphs/registry";
import type { GraphDefinition } from "../../graphs/types";
import { apiGet } from "../../lib/api";
import { mapGraphDefinitionResponse } from "./mapGraphDefinitionResponse";

export type GraphDefinitionSource = "api" | "registry" | "none";

export type UseGraphDefinitionResult = {
  definition: GraphDefinition | null;
  loading: boolean;
  error: unknown;
  /** 最終的にどの経路の定義を使ったか */
  source: GraphDefinitionSource;
};

/**
 * graphId に対する描画用 GraphDefinition。
 * まず GET /v1/graphs/{graphId}（プロキシ: /api/core/graphs/...）を試し、
 * 失敗時または空レスポンス時はローカル registry にフォールバックする（3.3）。
 */
export function useGraphDefinition(graphId: string | null): UseGraphDefinitionResult {
  const [definition, setDefinition] = useState<GraphDefinition | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [source, setSource] = useState<GraphDefinitionSource>("none");

  useEffect(() => {
    if (!graphId) {
      setDefinition(null);
      setError(null);
      setSource("none");
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    const fallback = (): GraphDefinition | null => getGraphDefinition(graphId);

    apiGet<unknown>(`/graphs/${encodeURIComponent(graphId)}`)
      .then((raw) => {
        if (cancelled) return;
        const mapped = mapGraphDefinitionResponse(raw, graphId);
        if (mapped) {
          setDefinition(mapped);
          setSource("api");
          return;
        }
        const reg = fallback();
        setDefinition(reg);
        setSource(reg ? "registry" : "none");
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err);
        const reg = fallback();
        setDefinition(reg);
        setSource(reg ? "registry" : "none");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [graphId]);

  return { definition, loading, error, source };
}
