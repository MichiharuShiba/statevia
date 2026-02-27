"use client";

import { useEffect, useState } from "react";
import { apiGet } from "../../lib/api";
import type { ExecutionEventsResponse } from "../../lib/types";

const DEFAULT_LIMIT = 500;

export function useExecutionEvents(executionId: string | null, options?: { limit?: number }) {
  const limit = options?.limit ?? DEFAULT_LIMIT;
  const [events, setEvents] = useState<ExecutionEventsResponse["events"]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>(null);

  useEffect(() => {
    if (!executionId) {
      setEvents([]);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    apiGet<ExecutionEventsResponse>(
      `/executions/${encodeURIComponent(executionId)}/events?limit=${limit}`
    )
      .then((res) => {
        if (!cancelled) setEvents(res.events ?? []);
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

  return { events, loading, error };
}
