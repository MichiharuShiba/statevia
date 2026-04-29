"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ListPagination } from "../components/layout/ListPagination";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import { apiGet } from "../lib/api";
import { formatDateTimeLocalized } from "../lib/dateTime";
import { toToastError, type ToastState } from "../lib/errors";
import { getDateTimeLocale } from "../lib/i18n";
import type { DefinitionDTO, PagedDefinitions } from "../lib/types";
import { useI18n } from "../lib/uiTextContext";

const PAGE_SIZE = 20;

/**
 * Definition 一覧（検索・ページング）を表示する。
 */
export function DefinitionsPageClient() {
  const { uiText, locale } = useI18n();
  const dateTimeLocale = getDateTimeLocale(locale);
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
      ariaLabel={uiText.definitionsPage.pagination.ariaLabel}
      currentPageLabel={uiText.definitionsPage.pagination.currentPage(currentPage)}
      hasPrev={hasPrev}
      hasNext={hasNext}
      onPrev={() => setCurrentPage((page) => Math.max(1, page - 1))}
      onNext={() => setCurrentPage((page) => page + 1)}
    />
  );

  return (
    <PageShell
      title={uiText.lists.definitions}
      description={uiText.definitionsPage.description}
    >

      <Toast toast={toast} onClose={() => setToast(null)} />

      <form className="flex flex-wrap items-end gap-3 rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4" onSubmit={handleSubmitSearch}>
        <label className="min-w-[260px] flex-1 text-sm">
          <span className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionsPage.search.label}</span>
          <input
            className="mt-1 w-full rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm text-[var(--md-sys-color-on-surface)]"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder={uiText.definitionsPage.search.placeholder}
          />
        </label>
        <button
          type="submit"
          className="rounded bg-[var(--md-sys-color-primary)] px-4 py-2 text-sm font-medium text-[var(--md-sys-color-on-primary)] hover:opacity-90"
          disabled={loading}
        >
          {uiText.definitionsPage.search.submit}
        </button>
        <button
          type="button"
          className="rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-4 py-2 text-sm text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)]"
          onClick={() => {
            setSearchInput("");
            setSubmittedSearch("");
            setCurrentPage(1);
          }}
          disabled={loading && !submittedSearch}
        >
          {uiText.definitionsPage.search.clear}
        </button>
        <Link
          href="/definitions/new"
          className="rounded bg-[var(--md-sys-color-primary)] px-4 py-2 text-sm font-medium text-[var(--md-sys-color-on-primary)] hover:opacity-90"
        >
          {uiText.definitionsPage.actions.createNew}
        </Link>
      </form>

      {loading && (
        <PageState state="loading" message={uiText.definitionsPage.loading} />
      )}

      {empty && (
        <PageState
          state="empty"
          message={uiText.definitionsPage.emptyNoMatch}
        />
      )}

      {!loading && items !== null && items.length > 0 && (
        <section aria-label={uiText.lists.definitions}>
          <div className="mb-2 flex items-center justify-between gap-3">
            <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
              {submittedSearch ? uiText.definitionsPage.searchSummaryPrefix(submittedSearch) : ""}
              {uiText.definitionsPage.listSummary(totalCount ?? 0, currentPage)}
            </p>
            {paginationNav}
          </div>
          <ul className="divide-y divide-[var(--md-sys-color-outline)] overflow-hidden rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] shadow-sm">
            {items.map((definition) => (
              <li key={definition.displayId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-[var(--md-sys-color-on-surface)]" title={definition.name}>
                    {definition.name}
                  </p>
                  <p className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
                    {uiText.definitionsPage.displayIdAndCreatedAt(
                      uiText.labels.displayId,
                      definition.displayId,
                      uiText.definitionsPage.createdAt(formatDateTimeLocalized(definition.createdAt, dateTimeLocale))
                    )}
                  </p>
                </div>
                <Link
                  href={`/definitions/${encodeURIComponent(definition.displayId)}`}
                  className="shrink-0 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-sm text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)]"
                >
                  {uiText.definitionsPage.actions.openDetail}
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
        <PageState state="error" message={uiText.definitionsPage.error} onRetry={() => void loadDefinitions()} />
      )}

    </PageShell>
  );
}
