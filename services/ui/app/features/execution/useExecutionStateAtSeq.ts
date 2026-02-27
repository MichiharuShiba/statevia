"use client";

import { useEffect, useState } from "react";
import { apiGet } from "../../lib/api";
import type { ExecutionDTO } from "../../lib/types";

/**
 * 指定 seq 時点の実行状態を取得（リプレイ表示用）。
 * atSeq が null の場合は何も取得しない。
 */
export function useExecutionStateAtSeq(
  executionId: string | null,
  atSeq: number | null
): { state: ExecutionDTO | null; loading: boolean; error: unknown } {
  const [state, setState] = useState<ExecutionDTO | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    if (!executionId || atSeq === null || atSeq < 1) {
      setState(null);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    apiGet<ExecutionDTO>(
      `/executions/${encodeURIComponent(executionId)}/state?atSeq=${atSeq}`
    )
      .then((res) => {
        if (!cancelled) setState(res);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err);
          setState(null);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [executionId, atSeq]);

  return { state, loading, error };
}
