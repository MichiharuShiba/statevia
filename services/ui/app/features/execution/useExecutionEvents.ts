"use client";

import { useCallback, useEffect, useState } from "react";
import { apiGet } from "../../lib/api";
import type { ExecutionEventWithSeq, ExecutionEventsResponse } from "../../lib/types";

const DEFAULT_LIMIT = 500;

export function useExecutionEvents(executionId: string | null, options?: { limit?: number }) {
  const limit = options?.limit ?? DEFAULT_LIMIT;
  const [events, setEvents] = useState<ExecutionEventWithSeq[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const fetchChunk = useCallback(
    async (afterSeq: number, append: boolean) => {
      if (!executionId) return;
      const setLoader = append ? setLoadingMore : setLoading;
      setLoader(true);
      if (!append) setError(null);

      try {
        const q = new URLSearchParams({ limit: String(limit) });
        if (afterSeq > 0) q.set("afterSeq", String(afterSeq));
        const res = await apiGet<ExecutionEventsResponse>(
          `/workflows/${encodeURIComponent(executionId)}/events?${q.toString()}`
        );
        const list = res.events ?? [];
        setHasMore(res.hasMore === true);
        setEvents((prev) => (append ? [...prev, ...list] : list));
      } catch (err) {
        if (!append) {
          setError(err);
          setEvents([]);
        }
      } finally {
        setLoader(false);
      }
    },
    [executionId, limit]
  );

  useEffect(() => {
    if (!executionId) {
      setEvents([]);
      setHasMore(false);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    const q = new URLSearchParams({ limit: String(limit) });
    apiGet<ExecutionEventsResponse>(
      `/workflows/${encodeURIComponent(executionId)}/events?${q.toString()}`
    )
      .then((res) => {
        if (!cancelled) {
          setEvents(res.events ?? []);
          setHasMore(res.hasMore === true);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err);
          setEvents([]);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [executionId, limit]);

  const loadMore = useCallback(() => {
    if (!executionId || loading || loadingMore || !hasMore) return;
    const lastSeq = events.length > 0 ? events[events.length - 1].seq : 0;
    if (lastSeq <= 0) return;
    fetchChunk(lastSeq, true);
  }, [executionId, events, hasMore, loading, loadingMore, fetchChunk]);

  return { events, hasMore, loading, loadingMore, error, loadMore };
}
