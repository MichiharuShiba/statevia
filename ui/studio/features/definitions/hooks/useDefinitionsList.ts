"use client";

import { useCallback, useEffect, useState } from "react";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { listDefinitions, type DefinitionsListQuery } from "../api";
import type { DefinitionDTO } from "../types";

/** useDefinitionsList の戻り値。 */
export type UseDefinitionsListResult = {
  items: DefinitionDTO[] | null;
  totalCount: number | null;
  loading: boolean;
  toast: ToastState | null;
  setToast: (toast: ToastState | null) => void;
  loadDefinitions: (options?: { clearToast?: boolean }) => Promise<void>;
  hasPrev: boolean;
  hasNext: boolean;
  empty: boolean;
};

/**
 * 定義一覧の取得状態を管理する。
 * @param listQuery URL 由来の一覧クエリ
 */
export function useDefinitionsList(listQuery: DefinitionsListQuery): UseDefinitionsListResult {
  const [items, setItems] = useState<DefinitionDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  /**
   * 定義一覧を再取得する。
   * @param options.clearToast 取得開始時にトーストを消すか（既定 true）
   */
  const loadDefinitions = useCallback(
    async (options?: { clearToast?: boolean }) => {
      setLoading(true);
      if (options?.clearToast !== false) {
        setToast(null);
      }
      try {
        const page = await listDefinitions(listQuery);
        setItems(page.items);
        setTotalCount(page.totalCount);
      } catch (error) {
        setToast(toToastError(error));
        setItems(null);
        setTotalCount(null);
      } finally {
        setLoading(false);
      }
    },
    [listQuery],
  );

  useEffect(() => {
    void loadDefinitions({ clearToast: true });
  }, [loadDefinitions]);

  const hasPrev = listQuery.pagination.offset > 0;
  const hasNext =
    totalCount !== null && listQuery.pagination.offset + (items?.length ?? 0) < totalCount;
  const empty = !loading && items !== null && items.length === 0;

  return {
    items,
    totalCount,
    loading,
    toast,
    setToast,
    loadDefinitions,
    hasPrev,
    hasNext,
    empty,
  };
}
