"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useState, Suspense } from "react";
import { StatusBadge } from "../components/common/StatusBadge";
import { ListPagination } from "../components/layout/ListPagination";
import { Toast } from "../components/Toast";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { apiGet, buildWorkflowsListPath, type WorkflowsListQuery } from "../lib/api";
import { toToastError, type ToastState } from "../lib/errors";
import type { PagedWorkflows, WorkflowDTO } from "../lib/types";
import { uiText } from "../lib/uiText";

const DEFAULT_LIMIT = 20;
const MAX_LIMIT = 500;
type StatusFilter = "" | "Running" | "Completed" | "Cancelled" | "Failed";

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("ja-JP", { dateStyle: "short", timeStyle: "short" });
}

/**
 * クエリから一覧の取得条件を正規化する。無効な値は既定に寄せる。
 */
function readListQuery(searchParams: { get: (name: string) => string | null }): WorkflowsListQuery {
  const limitRaw = Number.parseInt(searchParams.get("limit") ?? "", 10);
  const limit = Number.isFinite(limitRaw) ? Math.min(MAX_LIMIT, Math.max(1, limitRaw)) : DEFAULT_LIMIT;
  const offsetRaw = Number.parseInt(searchParams.get("offset") ?? "0", 10);
  const offset = Number.isFinite(offsetRaw) && offsetRaw >= 0 ? offsetRaw : 0;
  const statusRaw = searchParams.get("status")?.trim() ?? "";
  const asStatus: WorkflowsListQuery["status"] =
    statusRaw === "Running" || statusRaw === "Completed" || statusRaw === "Cancelled" || statusRaw === "Failed" ? statusRaw : undefined;
  const name = searchParams.get("name")?.trim() ?? "";
  const definitionId = searchParams.get("definitionId")?.trim() ?? "";
  return {
    limit,
    offset,
    status: asStatus,
    name: name || undefined,
    definitionId: definitionId || undefined
  };
}

/**
 * ページング・フィルタ（URL 同期）付きのワークフロー一覧。詳細は <code>/workflows/[id]</code> へ遷移する（T5）。
 */
function WorkflowsPageClientInner() {
  const searchParams = useSearchParams();
  const router = useRouter();

  const [items, setItems] = useState<WorkflowDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  const [nameDraft, setNameDraft] = useState("");
  const [definitionDraft, setDefinitionDraft] = useState("");

  const listQuery = useMemo(() => readListQuery(searchParams), [searchParams]);

  useEffect(() => {
    setNameDraft(listQuery.name ?? "");
    setDefinitionDraft(listQuery.definitionId ?? "");
  }, [listQuery.name, listQuery.definitionId]);

  const currentStatus = (listQuery.status ?? "") as StatusFilter;

  const currentPage1Based = useMemo(() => Math.floor(listQuery.offset / listQuery.limit) + 1, [listQuery.offset, listQuery.limit]);
  const hasPrev = listQuery.offset > 0;
  const hasNext = totalCount !== null && listQuery.offset + (items?.length ?? 0) < totalCount;

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const path = buildWorkflowsListPath({
        limit: listQuery.limit,
        offset: listQuery.offset,
        status: listQuery.status,
        name: listQuery.name,
        definitionId: listQuery.definitionId
      });
      const page = await apiGet<PagedWorkflows>(path);
      setItems(page.items);
      setTotalCount(page.totalCount);
    } catch (e) {
      setToast(toToastError(e));
      setItems(null);
      setTotalCount(null);
    } finally {
      setLoading(false);
    }
  }, [listQuery]);

  useEffect(() => {
    void load();
  }, [load]);

  const goTo = useCallback(
    (query: WorkflowsListQuery) => {
      router.replace(buildWorkflowsListPath(query), { scroll: false });
    },
    [router]
  );

  const handleFilterSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    goTo({
      limit: listQuery.limit,
      offset: 0,
      status: (currentStatus || undefined) as WorkflowsListQuery["status"],
      name: nameDraft || undefined,
      definitionId: definitionDraft || undefined
    });
  };
  const pagination = (
    <ListPagination
      ariaLabel={uiText.workflowsPage.pagination.ariaLabel}
      currentPageLabel={uiText.workflowsPage.pagination.currentPage(currentPage1Based)}
      hasPrev={hasPrev}
      hasNext={hasNext}
      prevLabel={uiText.workflowsPage.pagination.prev}
      nextLabel={uiText.workflowsPage.pagination.next}
      onPrev={() =>
        goTo({
          ...listQuery,
          offset: Math.max(0, listQuery.offset - listQuery.limit)
        })
      }
      onNext={() =>
        goTo({
          ...listQuery,
          offset: listQuery.offset + listQuery.limit
        })
      }
    />
  );

  return (
    <PageShell title={uiText.lists.workflows}>

      {listQuery.definitionId && (
        <output className="block rounded border border-sky-200 bg-sky-50 px-3 py-2 text-sm text-sky-900" aria-live="polite">
          <span className="text-sky-800">{uiText.workflowsPage.filter.contextActivePrefix} </span>
          <span className="font-mono break-all">{listQuery.definitionId}</span>
          <button
            type="button"
            className="ml-2 text-sky-950 underline hover:no-underline"
            onClick={() => {
              setDefinitionDraft("");
              goTo({
                limit: listQuery.limit,
                offset: 0,
                status: (currentStatus || undefined) as WorkflowsListQuery["status"],
                name: nameDraft || undefined
              });
            }}
          >
            {uiText.workflowsPage.filter.clearDefinition}
          </button>
        </output>
      )}

      <form onSubmit={handleFilterSubmit} className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
        <h2 className="text-sm font-medium text-zinc-900">{uiText.workflowsPage.filter.title}</h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="block text-sm text-zinc-800">
            <span className="text-zinc-600">{uiText.labels.status}</span>
            <select
              className="mt-1 w-full rounded border border-zinc-300 bg-white px-2 py-1.5 text-sm"
              value={currentStatus}
              onChange={(e) => {
                const v = e.target.value as StatusFilter;
                goTo({
                  limit: listQuery.limit,
                  offset: 0,
                  status: v || undefined,
                  name: nameDraft || undefined,
                  definitionId: definitionDraft || undefined
                });
              }}
            >
              <option value="">{uiText.workflowsPage.filter.all}</option>
              <option value="Running">Running</option>
              <option value="Completed">Completed</option>
              <option value="Cancelled">Cancelled</option>
              <option value="Failed">Failed</option>
            </select>
          </label>
          <label className="block text-sm text-zinc-800">
            <span className="text-zinc-600">{uiText.workflowsPage.filter.definitionLabelWithHint(uiText.labels.definitionId)}</span>
            <input
              className="mt-1 w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-sm"
              value={definitionDraft}
              onChange={(e) => setDefinitionDraft(e.target.value)}
              placeholder={uiText.workflowsPage.filter.definitionPlaceholder}
              autoComplete="off"
            />
          </label>
        </div>
        <div className="flex flex-wrap items-end gap-3">
          <label className="min-w-[260px] flex-1 text-sm text-zinc-800">
            <span className="text-zinc-600">{uiText.workflowsPage.filter.nameInputHint}</span>
            <input
              className="mt-1 w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-sm"
              value={nameDraft}
              onChange={(e) => setNameDraft(e.target.value)}
              autoComplete="off"
            />
          </label>
          <button
            type="submit"
            className="rounded bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-800"
            disabled={loading}
          >
            {uiText.workflowsPage.filter.search}
          </button>
          <button
            type="button"
            className="rounded border border-zinc-300 bg-white px-4 py-2 text-sm text-zinc-700 hover:bg-zinc-50"
            onClick={() => {
              setNameDraft("");
              setDefinitionDraft("");
              goTo({
                limit: listQuery.limit,
                offset: 0
              });
            }}
            disabled={loading && !currentStatus && !nameDraft && !definitionDraft}
          >
            {uiText.workflowsPage.filter.clear}
          </button>
        </div>
        <p className="text-xs text-zinc-500">
          {uiText.workflowsPage.filter.pageInfo(listQuery.limit, listQuery.offset, currentPage1Based)}
        </p>
      </form>

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && (
        <PageState state="loading" message={uiText.workflowsPage.loading} />
      )}

      {!loading && items !== null && items.length > 0 && (
        <section aria-label={uiText.lists.workflows}>
          <div className="mb-2 flex items-center justify-between gap-3">
            <p className="text-xs text-zinc-500">{uiText.workflowsPage.listSummary(totalCount ?? 0, currentPage1Based)}</p>
            {pagination}
          </div>
          <ul
            className="divide-y divide-zinc-200 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm"
            aria-label={uiText.lists.workflows}
          >
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
                    <p className="mt-1 text-xs text-zinc-500">{uiText.workflowsPage.updatedAt(formatDateTime(updated))}</p>
                  </div>
                  <Link
                    className="shrink-0 rounded border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-800 hover:bg-zinc-50"
                    href={`/workflows/${encodeURIComponent(workflow.displayId)}`}
                  >
                    {uiText.workflowsPage.actions.openDetail}
                  </Link>
                </li>
              );
            })}
          </ul>
          <div className="mt-2 flex justify-end">
            {pagination}
          </div>
        </section>
      )}

      {!loading && items !== null && items.length === 0 && (
        <PageState state="empty" message={uiText.workflowsPage.empty} />
      )}

      {!loading && !toast && items === null && (
        <PageState
          state="error"
          message={uiText.workflowsPage.error}
          onRetry={() => void load()}
        />
      )}

    </PageShell>
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
          {uiText.actions.loading}
        </div>
      }
    >
      <WorkflowsPageClientInner />
    </Suspense>
  );
}
