"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ListPagination } from "../components/layout/ListPagination";
import { NAVIGATION_BUTTON_CLASS } from "../components/layout/navigationButtonClass";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import { apiGet, buildDefinitionsListPath, type DefinitionsListQuery, type SortOrder } from "../lib/api";
import { formatDateTimeLocalized } from "../lib/dateTime";
import { toToastError, type ToastState } from "../lib/errors";
import { getDateTimeLocale } from "../lib/i18n";
import type { DefinitionDTO, PagedDefinitions } from "../lib/types";
import { useI18n } from "../lib/uiTextContext";
import { matchesPattern } from "../lib/validation/primitives";
import { SEARCH_NAME_PATTERN } from "../lib/validation/searchRules";

const PAGE_SIZE = 20;
type DefinitionsSortBy = "createdAt" | "name";

function readListQuery(searchParams: { get: (name: string) => string | null }): DefinitionsListQuery {
  const limitRaw = Number.parseInt(searchParams.get("limit") ?? "", 10);
  const limit = Number.isFinite(limitRaw) ? Math.max(1, limitRaw) : PAGE_SIZE;
  const offsetRaw = Number.parseInt(searchParams.get("offset") ?? "0", 10);
  const offset = Number.isFinite(offsetRaw) && offsetRaw >= 0 ? offsetRaw : 0;
  const name = searchParams.get("name")?.trim() ?? "";
  const sortByRaw = searchParams.get("sortBy")?.trim() ?? "";
  const sortOrderRaw = searchParams.get("sortOrder")?.trim() ?? "";
  const sortBy: DefinitionsSortBy = sortByRaw === "name" ? "name" : "createdAt";
  const sortOrder: SortOrder = sortOrderRaw === "asc" ? "asc" : "desc";
  return {
    pagination: { limit, offset },
    sort: { sortBy, sortOrder },
    name: name || undefined
  };
}

/**
 * Definition 一覧（検索・ページング）を表示する。
 */
export function DefinitionsPageClient() {
  const { uiText, locale } = useI18n();
  const router = useRouter();
  const searchParams = useSearchParams();
  const dateTimeLocale = getDateTimeLocale(locale);
  const [searchInput, setSearchInput] = useState("");
  const [items, setItems] = useState<DefinitionDTO[] | null>(null);
  const [totalCount, setTotalCount] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  const listQuery = useMemo(() => readListQuery(searchParams), [searchParams]);
  const currentPage = useMemo(
    () => Math.floor(listQuery.pagination.offset / listQuery.pagination.limit) + 1,
    [listQuery.pagination.limit, listQuery.pagination.offset]
  );
  const hasPrev = listQuery.pagination.offset > 0;
  const hasNext = totalCount !== null && listQuery.pagination.offset + (items?.length ?? 0) < totalCount;
  const effectiveSortBy: DefinitionsSortBy = listQuery.sort.sortBy === "name" ? "name" : "createdAt";
  const effectiveSortOrder: SortOrder = listQuery.sort.sortOrder ?? "desc";

  useEffect(() => {
    setSearchInput(listQuery.name ?? "");
  }, [listQuery.name]);

  const loadDefinitions = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const path = buildDefinitionsListPath(listQuery);
      const page = await apiGet<PagedDefinitions>(path);
      setItems(page.items);
      setTotalCount(page.totalCount);
    } catch (error) {
      setToast(toToastError(error));
      setItems(null);
      setTotalCount(null);
    } finally {
      setLoading(false);
    }
  }, [listQuery]);

  useEffect(() => {
    void loadDefinitions();
  }, [loadDefinitions]);

  const goTo = useCallback(
    (query: DefinitionsListQuery) => {
      router.replace(buildDefinitionsListPath(query), { scroll: false });
    },
    [router]
  );

  const handleSubmitSearch = useCallback(
    (event: React.FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      const trimmedKeyword = searchInput.trim();
      if (!matchesPattern(trimmedKeyword, SEARCH_NAME_PATTERN)) {
        setToast({
          tone: "error",
          message: uiText.definitionsPage.search.invalidName
        });
        return;
      }
      goTo({
        pagination: { ...listQuery.pagination, offset: 0 },
        sort: listQuery.sort,
        name: trimmedKeyword || undefined
      });
    },
    [goTo, listQuery.pagination, listQuery.sort, searchInput]
  );

  const empty = !loading && items !== null && items.length === 0;
  const paginationNav = (
    <ListPagination
      ariaLabel={uiText.definitionsPage.pagination.ariaLabel}
      currentPageLabel={uiText.definitionsPage.pagination.currentPage(currentPage)}
      hasPrev={hasPrev}
      hasNext={hasNext}
      onPrev={() =>
        goTo({
          ...listQuery,
          pagination: {
            ...listQuery.pagination,
            offset: Math.max(0, listQuery.pagination.offset - listQuery.pagination.limit)
          }
        })
      }
      onNext={() =>
        goTo({
          ...listQuery,
          pagination: {
            ...listQuery.pagination,
            offset: listQuery.pagination.offset + listQuery.pagination.limit
          }
        })
      }
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
          className="rounded border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] px-4 py-2 text-sm font-medium text-[var(--brand-cta-fg)] hover:bg-[var(--brand-cta-bg-hover)]"
          disabled={loading}
        >
          {uiText.definitionsPage.search.submit}
        </button>
        <button
          type="button"
          className="rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-4 py-2 text-sm text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)]"
          onClick={() => {
            setSearchInput("");
            goTo({
              pagination: { ...listQuery.pagination, offset: 0 },
              sort: listQuery.sort
            });
          }}
          disabled={loading && !listQuery.name}
        >
          {uiText.definitionsPage.search.clear}
        </button>
        <label className="text-sm">
          <span className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionsPage.sortByLabel}</span>
          <select
            className="mt-1 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm text-[var(--md-sys-color-on-surface)]"
              value={effectiveSortBy}
              onChange={(event) =>
                goTo({
                  ...listQuery,
                  pagination: { ...listQuery.pagination, offset: 0 },
                  sort: { ...listQuery.sort, sortBy: event.target.value as DefinitionsSortBy }
                })
              }
          >
            <option value="createdAt">{uiText.definitionsPage.sortByCreatedAt}</option>
            <option value="name">{uiText.definitionsPage.sortByName}</option>
          </select>
        </label>
        <label className="text-sm">
          <span className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionsPage.sortOrderLabel}</span>
          <select
            className="mt-1 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm text-[var(--md-sys-color-on-surface)]"
              value={effectiveSortOrder}
              onChange={(event) =>
                goTo({
                  ...listQuery,
                  pagination: { ...listQuery.pagination, offset: 0 },
                  sort: { ...listQuery.sort, sortOrder: event.target.value as SortOrder }
                })
              }
          >
            <option value="desc">{uiText.definitionsPage.sortOrderDesc}</option>
            <option value="asc">{uiText.definitionsPage.sortOrderAsc}</option>
          </select>
        </label>
        <button
          type="button"
          className="rounded border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] px-4 py-2 text-sm font-medium text-[var(--brand-cta-fg)] hover:bg-[var(--brand-cta-bg-hover)]"
          onClick={() => router.push("/definitions/new")}
        >
          {uiText.definitionsPage.actions.createNew}
        </button>
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
              {listQuery.name ? uiText.definitionsPage.searchSummaryPrefix(listQuery.name) : ""}
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
                <button
                  type="button"
                  className={`shrink-0 ${NAVIGATION_BUTTON_CLASS}`}
                  onClick={() => router.push(`/definitions/${encodeURIComponent(definition.displayId)}`)}
                >
                  {uiText.definitionsPage.actions.openDetail}
                </button>
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
