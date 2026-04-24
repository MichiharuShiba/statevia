"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { StatusBadge } from "../components/common/StatusBadge";
import { TenantMissingBanner } from "../components/execution/TenantMissingBanner";
import { ActionLinkGroup } from "../components/layout/ActionLinkGroup";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import { apiGet } from "../lib/api";
import { toToastError, type ToastState } from "../lib/errors";
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
  const actionLinks = [
    { label: "Workflow 一覧", href: "/workflows" },
    { label: "Definition 一覧", href: "/definitions" }
  ] as const;
  const totalCountLabel = totalCount == null ? null : <p className="text-xs">合計件数: {totalCount}</p>;

  return (
    <PageShell
      title="ダッシュボード"
      description="直近のワークフロー（最大 10 件）です。"
      primaryActions={<ActionLinkGroup links={[...actionLinks]} />}
      secondaryActions={totalCountLabel}
    >
      <TenantMissingBanner />

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && (
        <PageState state="loading" message="直近ワークフローを取得しています。" />
      )}

      {empty && (
        <PageState state="empty" message="Definition 一覧または Workflow 一覧から操作を開始できます。" />
      )}

      {!loading && items !== null && items.length > 0 && (
        <section aria-label="直近ワークフロー一覧">
          <ul className="divide-y divide-zinc-200 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm">
            {items.map((workflow) => {
              const updated = workflow.updatedAt ?? workflow.startedAt;
              return (
                <li key={workflow.displayId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <StatusBadge status={workflow.status} />
                      <span className="truncate font-mono text-sm text-zinc-900" title={workflow.displayId}>
                        {workflow.displayId}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-zinc-500">更新: {formatDateTime(updated)}</p>
                  </div>
                  <Link
                    className="shrink-0 rounded border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-800 hover:bg-zinc-50"
                    href={`/workflows/${encodeURIComponent(workflow.displayId)}`}
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
        <PageState state="error" message="データを取得できませんでした。" onRetry={() => void load()} />
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
    </PageShell>
  );
}
