"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { useDelayedVisibility } from "@/shared/lib/useDelayedVisibility";
import { toToastError, type ToastState } from "@/shared/lib/errors";
import { getDefinition, softDeleteDefinition } from "../api";
import type { DefinitionDTO } from "../types";

/** useDefinitionDetail のオプション。 */
export type UseDefinitionDetailOptions = {
  /** 削除成功時のトースト文言。 */
  deletedMessage: string;
  /** 削除成功後の遷移先（既定 `/definitions`）。 */
  afterDeleteHref?: string;
};

/** useDefinitionDetail の戻り値。 */
export type UseDefinitionDetailResult = {
  row: DefinitionDTO | null;
  loading: boolean;
  showLoading: boolean;
  toast: ToastState | null;
  setToast: (toast: ToastState | null) => void;
  confirmDelete: boolean;
  setConfirmDelete: (value: boolean) => void;
  deleting: boolean;
  load: () => Promise<void>;
  handleDeleteClick: () => Promise<void>;
};

/**
 * 定義詳細の取得と論理削除を管理する。
 * @param definitionId 対象 displayId
 * @param options 文言・遷移先
 */
export function useDefinitionDetail(
  definitionId: string,
  options: UseDefinitionDetailOptions,
): UseDefinitionDetailResult {
  const { deletedMessage, afterDeleteHref = "/definitions" } = options;
  const router = useRouter();
  const [row, setRow] = useState<DefinitionDTO | null>(null);
  const [loading, setLoading] = useState(true);
  const showLoading = useDelayedVisibility(loading);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const definition = await getDefinition(definitionId);
      setRow(definition);
    } catch (error) {
      setToast(toToastError(error));
      setRow(null);
    } finally {
      setLoading(false);
    }
  }, [definitionId]);

  useEffect(() => {
    void load();
  }, [load]);

  /**
   * 論理削除のインライン二段階確認を進める。
   * UI 側は戻り Promise を `.catch` で扱う（エラー表示は本関数内で完結）。
   */
  const handleDeleteClick = useCallback(async () => {
    if (!confirmDelete) {
      setConfirmDelete(true);
      return;
    }
    setDeleting(true);
    setConfirmDelete(false);
    try {
      await softDeleteDefinition(definitionId);
      setToast({ tone: "success", message: deletedMessage });
      router.push(afterDeleteHref);
    } catch (error) {
      setToast(toToastError(error));
      setDeleting(false);
    }
  }, [afterDeleteHref, confirmDelete, definitionId, deletedMessage, router]);

  return {
    row,
    loading,
    showLoading,
    toast,
    setToast,
    confirmDelete,
    setConfirmDelete,
    deleting,
    load,
    handleDeleteClick,
  };
}
