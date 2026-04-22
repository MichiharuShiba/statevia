"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { TenantMissingBanner } from "../components/execution/TenantMissingBanner";
import { Toast } from "../components/Toast";
import { apiGet } from "../lib/api";
import { toToastError, type ToastState } from "../lib/errors";
import { getStatusStyle, type StatusLike } from "../lib/statusStyle";
import type { PagedWorkflows, WorkflowDTO } from "../lib/types";

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return date.toLocaleString("ja-JP", { dateStyle: "short", timeStyle: "short" });
}

/**
 * 直近ワークフロー 10 件のダッシュボード（一覧取得・空状態・詳細への導線）。
 */
export function DashboardPageClient() {
  const [items, setItems] = useState<WorkflowDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const page = await apiGet<PagedWorkflows>("/workflows?limit=10&offset=0");
      setItems(page.items);
      setTotalCount(page.totalCount);
    } catch (error) {
      setToast(toToastError(error));
      setItems(null);
      setTotalCount(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const empty = !loading && items !== null && items.length === 0;

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-6 p-6">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold text-zinc-900">ダッシュボード</h1>
          <p className="mt-1 text-sm text-zinc-600">直近のワークフロー（最大 10 件）です。</p>
          {totalCount !== null && (
            <p className="mt-1 text-xs text-zinc-500">テナント内の合計件数: {totalCount}</p>
          )}
        </div>
        <div className="flex flex-wrap gap-3 text-sm">
          <Link className="text-blue-700 underline hover:text-blue-900" href="/definitions">
            Definition 一覧
          </Link>
          <Link className="text-blue-700 underline hover:text-blue-900" href="/playground">
            Playground
          </Link>
        </div>
      </header>

      <TenantMissingBanner />

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && (
        <output className="block text-sm text-zinc-500" aria-live="polite">
          読み込み中…
        </output>
      )}

      {empty && (
        <section
          className="rounded-lg border border-dashed border-zinc-300 bg-zinc-50 p-6 text-sm text-zinc-700"
          aria-label="直近ワークフローなし"
        >
          <p className="font-medium text-zinc-800">直近のワークフローはありません。</p>
          <p className="mt-2 text-zinc-600">次の導線から操作を開始できます。</p>
          <ul className="mt-3 list-inside list-disc space-y-1">
            <li>
              <Link className="text-blue-700 underline" href="/definitions">
                Definition 一覧
              </Link>
            </li>
            <li>
              <Link className="text-blue-700 underline" href="/playground">
                定義登録（Playground）
              </Link>
            </li>
            <li>
              <span className="text-zinc-600">
                ワークフロー一覧（検索・ページング）は別タスクで <code className="rounded bg-zinc-200 px-1">/workflows</code> に追加予定です。
              </span>
            </li>
          </ul>
        </section>
      )}

      {!loading && items !== null && items.length > 0 && (
        <section aria-label="直近ワークフロー一覧">
          <ul className="divide-y divide-zinc-200 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm">
            {items.map((workflow) => {
              const style = getStatusStyle(workflow.status as StatusLike);
              const updated = workflow.updatedAt ?? workflow.startedAt;
              return (
                <li key={workflow.displayId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${style.badgeClass}`}>
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
                    詳細を開く
                  </Link>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      {!loading && items === null && !toast && (
        <p className="text-sm text-zinc-600">データを取得できませんでした。</p>
      )}

      {!loading && (
        <button
          type="button"
          className="self-start text-sm text-blue-700 underline hover:text-blue-900"
          onClick={() => void load()}
        >
          再読み込み
        </button>
      )}
    </div>
  );
}
