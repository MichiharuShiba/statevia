"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { ListPagination } from "../components/layout/ListPagination";
import { NAVIGATION_BUTTON_CLASS } from "../components/layout/navigationButtonClass";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import {
  apiDelete,
  apiGet,
  apiPost,
  buildDefinitionsListPath,
  type DefinitionsListQuery,
  type SortOrder
} from "../lib/api";
import { formatDateTimeLocalized } from "../lib/dateTime";
import { toToastError, type ToastState } from "../lib/errors";
import { getDateTimeLocale } from "../lib/i18n";
import type { DefinitionDTO, PagedDefinitions } from "../lib/types";
import { useI18n } from "../lib/uiTextContext";
import { matchesPattern } from "../lib/validation/primitives";
import { SEARCH_NAME_PATTERN } from "../lib/validation/searchRules";

const PAGE_SIZE = 20;
type DefinitionsSortBy = "createdAt" | "name";

/** 行単位のインライン確認対象。 */
type PendingConfirm =
  | { kind: "delete"; displayId: string }
  | { kind: "restore"; displayId: string };

/**
 * 定義が catalog 上削除済みかどうかを判定する。
 * @param definition 一覧要素
 * @returns `deletedAt` が truthy のとき true
 */
function isDeletedDefinition(definition: DefinitionDTO): boolean {
  return Boolean(definition.deletedAt);
}

/**
 * URL searchParams から定義一覧クエリを読む。
 * @param searchParams Next.js searchParams
 * @returns DefinitionsListQuery
 */
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
  const includeDeleted = searchParams.get("includeDeleted") === "true";
  return {
    pagination: { limit, offset },
    sort: { sortBy, sortOrder },
    name: name || undefined,
    includeDeleted: includeDeleted || undefined
  };
}

/**
 * Definition 一覧（検索・ページング・catalog 論理削除/復元）を表示する。
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
  const [pendingConfirm, setPendingConfirm] = useState<PendingConfirm | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [restoringId, setRestoringId] = useState<string | null>(null);

  const listQuery = useMemo(() => readListQuery(searchParams), [searchParams]);
  const currentPage = useMemo(
    () => Math.floor(listQuery.pagination.offset / listQuery.pagination.limit) + 1,
    [listQuery.pagination.limit, listQuery.pagination.offset]
  );
  const hasPrev = listQuery.pagination.offset > 0;
  const hasNext = totalCount !== null && listQuery.pagination.offset + (items?.length ?? 0) < totalCount;
  const effectiveSortBy: DefinitionsSortBy = listQuery.sort.sortBy === "name" ? "name" : "createdAt";
  const effectiveSortOrder: SortOrder = listQuery.sort.sortOrder ?? "desc";
  const includeDeleted = listQuery.includeDeleted === true;

  useEffect(() => {
    setSearchInput(listQuery.name ?? "");
  }, [listQuery.name]);

  /**
   * 定義一覧を再取得する。
   * @param options.clearToast 取得開始時にトーストを消すか（既定 true）
   */
  const loadDefinitions = useCallback(async (options?: { clearToast?: boolean }) => {
    setLoading(true);
    if (options?.clearToast !== false) {
      setToast(null);
    }
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
    void loadDefinitions({ clearToast: true });
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
        name: trimmedKeyword || undefined,
        includeDeleted: listQuery.includeDeleted
      });
    },
    [goTo, listQuery, searchInput, uiText.definitionsPage.search.invalidName]
  );

  const runDelete = useCallback(
    async (displayId: string) => {
      setDeletingId(displayId);
      setPendingConfirm(null);
      try {
        await apiDelete(`/definitions/${encodeURIComponent(displayId)}`);
        setToast({ tone: "success", message: uiText.definitionsPage.toasts.deleted });
        await loadDefinitions({ clearToast: false });
      } catch (error) {
        setToast(toToastError(error));
      } finally {
        setDeletingId(null);
      }
    },
    [loadDefinitions, uiText.definitionsPage.toasts.deleted]
  );

  const runRestore = useCallback(
    async (displayId: string) => {
      setRestoringId(displayId);
      setPendingConfirm(null);
      try {
        await apiPost(`/definitions/${encodeURIComponent(displayId)}/restore`, {});
        setToast({ tone: "success", message: uiText.definitionsPage.toasts.restored });
        await loadDefinitions({ clearToast: false });
      } catch (error) {
        setToast(toToastError(error));
      } finally {
        setRestoringId(null);
      }
    },
    [loadDefinitions, uiText.definitionsPage.toasts.restored]
  );

  /**
   * 削除のインライン二段階確認を進める。
   * @param displayId 対象定義の displayId
   */
  const handleDeleteClick = useCallback(
    (displayId: string) => {
      if (pendingConfirm?.kind === "delete" && pendingConfirm.displayId === displayId) {
        void runDelete(displayId);
        return;
      }
      setPendingConfirm({ kind: "delete", displayId });
    },
    [pendingConfirm, runDelete]
  );

  /**
   * 復元のインライン二段階確認を進める。
   * @param displayId 対象定義の displayId
   */
  const handleRestoreClick = useCallback(
    (displayId: string) => {
      if (pendingConfirm?.kind === "restore" && pendingConfirm.displayId === displayId) {
        void runRestore(displayId);
        return;
      }
      setPendingConfirm({ kind: "restore", displayId });
    },
    [pendingConfirm, runRestore]
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
              sort: listQuery.sort,
              includeDeleted: listQuery.includeDeleted
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
                sort: { ...listQuery.sort, sortBy: event.target.value }
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
        <label className="flex items-center gap-2 text-sm text-[var(--md-sys-color-on-surface)]">
          <input
            type="checkbox"
            checked={includeDeleted}
            onChange={(event) =>
              goTo({
                ...listQuery,
                pagination: { ...listQuery.pagination, offset: 0 },
                includeDeleted: event.target.checked || undefined
              })
            }
            disabled={loading}
          />
          <span>{uiText.definitionsPage.includeDeleted.label}</span>
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
            {items.map((definition) => {
              const deleted = isDeletedDefinition(definition);
              const isDeletePending =
                pendingConfirm?.kind === "delete" && pendingConfirm.displayId === definition.displayId;
              const isRestorePending =
                pendingConfirm?.kind === "restore" && pendingConfirm.displayId === definition.displayId;
              const isDeleting = deletingId === definition.displayId;
              const isRestoring = restoringId === definition.displayId;
              return (
                <li key={definition.displayId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <p className="flex flex-wrap items-center gap-2 truncate font-medium text-[var(--md-sys-color-on-surface)]" title={definition.name}>
                      <span className="truncate">{definition.name}</span>
                      {deleted && (
                        <span className="shrink-0 rounded border border-[var(--md-sys-color-outline-variant)] px-1.5 py-0.5 text-xs font-normal text-[var(--md-sys-color-on-surface-variant)]">
                          {uiText.definitionsPage.deletedBadge}
                        </span>
                      )}
                    </p>
                    <p className="mt-1 text-xs text-[var(--md-sys-color-on-surface-variant)]">
                      {uiText.definitionsPage.displayIdAndCreatedAt(
                        uiText.labels.displayId,
                        definition.displayId,
                        uiText.definitionsPage.createdAt(formatDateTimeLocalized(definition.createdAt, dateTimeLocale))
                      )}
                      {deleted && definition.deletedAt
                        ? ` / ${uiText.definitionsPage.deletedAt(formatDateTimeLocalized(definition.deletedAt, dateTimeLocale))}`
                        : ""}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-wrap items-center gap-2">
                    {!deleted && (
                      <>
                        <button
                          type="button"
                          className={NAVIGATION_BUTTON_CLASS}
                          onClick={() => router.push(`/definitions/${encodeURIComponent(definition.displayId)}`)}
                        >
                          {uiText.definitionsPage.actions.openDetail}
                        </button>
                        {isDeletePending ? (
                          <>
                            <button
                              type="button"
                              className="rounded border border-red-700 bg-red-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60"
                              disabled={isDeleting}
                              onClick={() => handleDeleteClick(definition.displayId)}
                            >
                              {isDeleting
                                ? uiText.definitionsPage.actions.deleting
                                : uiText.definitionsPage.actions.confirmDelete}
                            </button>
                            <button
                              type="button"
                              className="rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-sm text-[var(--md-sys-color-on-surface)]"
                              disabled={isDeleting}
                              onClick={() => setPendingConfirm(null)}
                            >
                              {uiText.definitionsPage.actions.cancelConfirm}
                            </button>
                          </>
                        ) : (
                          <button
                            type="button"
                            className="rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-sm text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)] disabled:opacity-60"
                            disabled={deletingId !== null || restoringId !== null}
                            onClick={() => handleDeleteClick(definition.displayId)}
                          >
                            {uiText.definitionsPage.actions.delete}
                          </button>
                        )}
                      </>
                    )}
                    {deleted && (
                      <>
                        {isRestorePending ? (
                          <>
                            <button
                              type="button"
                              className="rounded border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] px-3 py-1.5 text-sm font-medium text-[var(--brand-cta-fg)] hover:bg-[var(--brand-cta-bg-hover)] disabled:opacity-60"
                              disabled={isRestoring}
                              onClick={() => handleRestoreClick(definition.displayId)}
                            >
                              {isRestoring
                                ? uiText.definitionsPage.actions.restoring
                                : uiText.definitionsPage.actions.confirmRestore}
                            </button>
                            <button
                              type="button"
                              className="rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-sm text-[var(--md-sys-color-on-surface)]"
                              disabled={isRestoring}
                              onClick={() => setPendingConfirm(null)}
                            >
                              {uiText.definitionsPage.actions.cancelConfirm}
                            </button>
                          </>
                        ) : (
                          <button
                            type="button"
                            className="rounded border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] px-3 py-1.5 text-sm font-medium text-[var(--brand-cta-fg)] hover:bg-[var(--brand-cta-bg-hover)] disabled:opacity-60"
                            disabled={deletingId !== null || restoringId !== null}
                            onClick={() => handleRestoreClick(definition.displayId)}
                          >
                            {uiText.definitionsPage.actions.restore}
                          </button>
                        )}
                      </>
                    )}
                  </div>
                </li>
              );
            })}
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
