"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ListPagination } from "../components/layout/ListPagination";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import { apiGet } from "../lib/api";
import { toToastError, type ToastState } from "../lib/errors";
import type { DefinitionDTO, PagedDefinitions } from "../lib/types";
import { uiText } from "../lib/uiText";

const PAGE_SIZE = 20;

function formatDateTime(iso: string): string {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return iso;
  return parsed.toLocaleString("ja-JP", { dateStyle: "short", timeStyle: "short" });
}

/**
 * Definition 一覧（検索・ページング）を表示する。
 */
export function DefinitionsPageClient() {
  const [searchInput, setSearchInput] = useState("");
  const [submittedSearch, setSubmittedSearch] = useState("");
  const [currentPage, setCurrentPage] = useState(1);
  const [items, setItems] = useState<DefinitionDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  const offset = useMemo(() => (currentPage - 1) * PAGE_SIZE, [currentPage]);
  const hasPrev = currentPage > 1;
  const hasNext = totalCount !== null && offset + (items?.length ?? 0) < totalCount;

  const loadDefinitions = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const query = new URLSearchParams({
        limit: String(PAGE_SIZE),
        offset: String(offset)
      });
      const keyword = submittedSearch.trim();
      if (keyword) query.set("name", keyword);
      const page = await apiGet<PagedDefinitions>(`/definitions?${query.toString()}`);
      setItems(page.items);
      setTotalCount(page.totalCount);
    } catch (error) {
      setToast(toToastError(error));
      setItems(null);
      setTotalCount(null);
    } finally {
      setLoading(false);
    }
  }, [offset, submittedSearch]);

  useEffect(() => {
    void loadDefinitions();
  }, [loadDefinitions]);

  const handleSubmitSearch = useCallback(
    (event: React.FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      setCurrentPage(1);
      setSubmittedSearch(searchInput.trim());
    },
    [searchInput]
  );

  const empty = !loading && items !== null && items.length === 0;
  const paginationNav = (
    <ListPagination
      ariaLabel={`${uiText.lists.definitions}ページネーション`}
      currentPageLabel={`${currentPage} ページ目`}
      hasPrev={hasPrev}
      hasNext={hasNext}
      onPrev={() => setCurrentPage((page) => Math.max(1, page - 1))}
      onNext={() => setCurrentPage((page) => page + 1)}
    />
  );

  return (
    <PageShell
      title={uiText.lists.definitions}
      description="定義の検索とページングを行い、詳細画面へ遷移します。"
    >

      <Toast toast={toast} onClose={() => setToast(null)} />

      <form className="flex flex-wrap items-end gap-3 rounded-lg border border-zinc-200 bg-white p-4" onSubmit={handleSubmitSearch}>
        <label className="min-w-[260px] flex-1 text-sm">
          <span className="text-zinc-600">名前検索（部分一致）</span>
          <input
            className="mt-1 w-full rounded border border-zinc-300 px-3 py-2 text-sm"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="例: order"
          />
        </label>
        <button
          type="submit"
          className="rounded bg-zinc-900 px-4 py-2 text-sm font-medium text-white hover:bg-zinc-800"
          disabled={loading}
        >
          検索
        </button>
        <button
          type="button"
          className="rounded border border-zinc-300 bg-white px-4 py-2 text-sm text-zinc-700 hover:bg-zinc-50"
          onClick={() => {
            setSearchInput("");
            setSubmittedSearch("");
            setCurrentPage(1);
          }}
          disabled={loading && !submittedSearch}
        >
          クリア
        </button>
      </form>

      {loading && (
        <PageState state="loading" message="定義一覧を読み込み中です。" />
      )}

      {empty && (
        <PageState
          state="empty"
          message={`該当する${uiText.entities.definition}はありません。検索条件を変更するか、条件をクリアして再検索してください。`}
        />
      )}

      {!loading && items !== null && items.length > 0 && (
        <section aria-label={uiText.lists.definitions}>
          <div className="mb-2 flex items-center justify-between gap-3">
            <p className="text-xs text-zinc-500">
              {submittedSearch ? `検索: "${submittedSearch}" / ` : ""}
              合計 {totalCount ?? 0} 件（{currentPage} ページ目）
            </p>
            {paginationNav}
          </div>
          <ul className="divide-y divide-zinc-200 overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm">
            {items.map((definition) => (
              <li key={definition.displayId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-zinc-900" title={definition.name}>
                    {definition.name}
                  </p>
                  <p className="mt-1 text-xs text-zinc-500">
                    {uiText.labels.displayId}: <span className="font-mono">{definition.displayId}</span> / 作成: {formatDateTime(definition.createdAt)}
                  </p>
                </div>
                <Link
                  href={`/definitions/${encodeURIComponent(definition.displayId)}`}
                  className="shrink-0 rounded border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-800 hover:bg-zinc-50"
                >
                  詳細を開く
                </Link>
              </li>
            ))}
          </ul>
          <div className="mt-2 flex justify-end">
            {paginationNav}
          </div>
        </section>
      )}

      {!loading && items === null && !toast && (
        <PageState state="error" message="定義一覧を取得できませんでした。" onRetry={() => void loadDefinitions()} />
      )}

    </PageShell>
  );
}
