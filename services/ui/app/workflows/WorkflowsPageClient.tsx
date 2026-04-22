"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { Toast } from "../components/Toast";
import { apiGet } from "../lib/api";
import { toToastError, type ToastState } from "../lib/errors";
import { getStatusStyle, type StatusLike } from "../lib/statusStyle";
import type { PagedWorkflows, WorkflowDTO } from "../lib/types";
import { TenantMissingBanner } from "../components/execution/TenantMissingBanner";

const PAGE_SIZE = 20;

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("ja-JP", { dateStyle: "short", timeStyle: "short" });
}

/**
 * ページング付きでワークフロー一覧を表示し、詳細（現行: Playground run 画面）へ遷移する。
 * `definitionId` クエリは T5 で API 連携のフィルタに接続予定。現状は当該定義の文脈表示のみ行う。
 */
function WorkflowsPageClientInner() {
  const searchParams = useSearchParams();
  const definitionIdFromContext = searchParams.get("definitionId");
  const [currentPage, setCurrentPage] = useState(1);
  const [items, setItems] = useState<WorkflowDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  const offset = useMemo(() => (currentPage - 1) * PAGE_SIZE, [currentPage]);
  const hasPrev = currentPage > 1;
  const hasNext = totalCount !== null && offset + (items?.length ?? 0) < totalCount;

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const q = new URLSearchParams({ limit: String(PAGE_SIZE), offset: String(offset) });
      const page = await apiGet<PagedWorkflows>(`/workflows?${q.toString()}`);
      setItems(page.items);
      setTotalCount(page.totalCount);
    } catch (e) {
      setToast(toToastError(e));
      setItems(null);
      setTotalCount(null);
    } finally {
      setLoading(false);
    }
  }, [offset]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-5 p-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900">Workflow 一覧</h1>
          <p className="mt-1 text-sm text-zinc-600">Core-API のページング（limit/offset）で取得します（T5 で定義文脈フィルタ等を拡充予定）。</p>
          {totalCount !== null && (
            <p className="mt-1 text-xs text-zinc-500">合計件数: {totalCount}</p>
          )}
        </div>
        <div className="flex flex-wrap gap-3 text-sm">
          <Link className="text-blue-700 underline hover:text-blue-900" href="/dashboard">
            ダッシュボード
          </Link>
          <Link className="text-blue-700 underline hover:text-blue-900" href="/definitions">
            Definition 一覧
          </Link>
        </div>
      </header>

      {definitionIdFromContext && (
        <output className="block rounded border border-sky-200 bg-sky-50 px-3 py-2 text-sm text-sky-900" aria-live="polite">
          定義文脈: <span className="font-mono break-all">{definitionIdFromContext}</span>
        </output>
      )}

      <TenantMissingBanner />
      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && (
        <output className="text-sm text-zinc-500" aria-live="polite">
          読み込み中…
        </output>
      )}

      {!loading && items !== null && items.length > 0 && (
        <ul className="divide-y divide-zinc-200 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm" aria-label="ワークフロー一覧">
          {items.map((workflow) => {
            const st = getStatusStyle(workflow.status as StatusLike);
            const updated = workflow.updatedAt ?? workflow.startedAt;
            return (
              <li key={workflow.displayId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${st.badgeClass}`}>
                      {workflow.status}
                    </span>
                    <span className="truncate font-mono text-sm text-zinc-900" title={workflow.displayId}>
                      {workflow.displayId}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-zinc-500">更新: {formatDateTime(updated)}</p>
                </div>
                <Link
                  className="shrink-0 rounded border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-800 hover:bg-zinc-50"
                  href={`/playground/run/${encodeURIComponent(workflow.displayId)}`}
                >
                  開く
                </Link>
              </li>
            );
          })}
        </ul>
      )}

      {!loading && items !== null && items.length === 0 && (
        <p className="rounded border border-dashed border-zinc-300 bg-zinc-50 p-4 text-sm text-zinc-700">ワークフローがありません。</p>
      )}

      {!loading && !toast && items === null && (
        <p className="text-sm text-zinc-600">取得に失敗しました。テナント設定を確認するか、再試行してください。</p>
      )}

      {items !== null && (hasPrev || hasNext) && (
        <div className="flex flex-wrap items-center gap-3 text-sm text-zinc-800">
          <button
            type="button"
            className="rounded border border-zinc-300 bg-white px-2 py-1.5 text-zinc-800 hover:bg-zinc-50 disabled:opacity-40"
            onClick={() => setCurrentPage((n) => Math.max(1, n - 1))}
            disabled={!hasPrev}
          >
            前のページ
          </button>
          <span>ページ: {currentPage}</span>
          <button
            type="button"
            className="rounded border border-zinc-300 bg-white px-2 py-1.5 text-zinc-800 hover:bg-zinc-50 disabled:opacity-40"
            onClick={() => setCurrentPage((n) => n + 1)}
            disabled={!hasNext}
          >
            次のページ
          </button>
        </div>
      )}

      <button
        type="button"
        className="w-fit self-start text-sm text-blue-700 underline hover:text-blue-900"
        onClick={() => void load()}
      >
        再読み込み
      </button>
    </div>
  );
}

/**
 * ページング付き workflow 一覧（検索パラメータ用に `useSearchParams` 利用箇所を `Suspense` で包む）。
 */
export function WorkflowsPageClient() {
  return (
    <Suspense
      fallback={
        <div className="p-6 text-sm text-zinc-500" aria-live="polite">
          読み込み中…
        </div>
      }
    >
      <WorkflowsPageClientInner />
    </Suspense>
  );
}
