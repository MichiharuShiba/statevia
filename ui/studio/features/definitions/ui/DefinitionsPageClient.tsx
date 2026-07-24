"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import type { SortOrder } from "@/features/definitions/api";
import { useDefinitionCatalogActions } from "@/features/definitions/hooks/useDefinitionCatalogActions";
import { useDefinitionsList } from "@/features/definitions/hooks/useDefinitionsList";
import { useDefinitionsListQuery } from "@/features/definitions/hooks/useDefinitionsListQuery";
import { isDeletedDefinition } from "@/features/definitions/types";
import { getDateTimeLocale } from "@/shared/i18n/i18n";
import { useI18n } from "@/shared/i18n/uiTextContext";
import { formatDateTimeLocalized } from "@/shared/lib/dateTime";
import { matchesPattern } from "@/shared/lib/validation/primitives";
import { SEARCH_NAME_PATTERN } from "@/shared/lib/validation/searchRules";
import { ListPagination } from "@/shared/ui/ListPagination";
import { NAVIGATION_BUTTON_CLASS } from "@/shared/ui/navigationButtonClass";
import { PageShell } from "@/shared/ui/PageShell";
import { PageState } from "@/shared/ui/PageState";
import { Toast } from "@/shared/ui/Toast";

/**
 * Definition 一覧（検索・ページング・catalog 論理削除/復元）を表示する。
 */
export function DefinitionsPageClient() {
  const { uiText, locale } = useI18n();
  const router = useRouter();
  const dateTimeLocale = getDateTimeLocale(locale);
  const [searchInput, setSearchInput] = useState("");
  const {
    listQuery,
    currentPage,
    effectiveSortBy,
    effectiveSortOrder,
    includeDeleted,
    goTo,
  } = useDefinitionsListQuery();
  const {
    items,
    totalCount,
    loading,
    toast,
    setToast,
    loadDefinitions,
    hasPrev,
    hasNext,
    empty,
  } = useDefinitionsList(listQuery);
  const {
    pendingConfirm,
    setPendingConfirm,
    deletingId,
    restoringId,
    handleDeleteClick,
    handleRestoreClick,
  } = useDefinitionCatalogActions({
    reload: loadDefinitions,
    setToast,
    deletedMessage: uiText.definitionsPage.toasts.deleted,
    restoredMessage: uiText.definitionsPage.toasts.restored,
  });

  useEffect(() => {
    setSearchInput(listQuery.name ?? "");
  }, [listQuery.name]);

  const handleSubmitSearch = useCallback(
    (event: React.FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      const trimmedKeyword = searchInput.trim();
      if (!matchesPattern(trimmedKeyword, SEARCH_NAME_PATTERN)) {
        setToast({
          tone: "error",
          message: uiText.definitionsPage.search.invalidName,
        });
        return;
      }
      goTo({
        pagination: { ...listQuery.pagination, offset: 0 },
        sort: listQuery.sort,
        name: trimmedKeyword || undefined,
        includeDeleted: listQuery.includeDeleted,
      });
    },
    [goTo, listQuery, searchInput, setToast, uiText.definitionsPage.search.invalidName],
  );

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
            offset: Math.max(0, listQuery.pagination.offset - listQuery.pagination.limit),
          },
        })
      }
      onNext={() =>
        goTo({
          ...listQuery,
          pagination: {
            ...listQuery.pagination,
            offset: listQuery.pagination.offset + listQuery.pagination.limit,
          },
        })
      }
    />
  );

  return (
    <PageShell title={uiText.lists.definitions} description={uiText.definitionsPage.description}>
      <Toast toast={toast} onClose={() => setToast(null)} />

      <form
        className="flex flex-wrap items-end gap-3 rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4"
        onSubmit={handleSubmitSearch}
      >
        <label className="min-w-[260px] flex-1 text-sm">
          <span className="text-[var(--md-sys-color-on-surface-variant)]">
            {uiText.definitionsPage.search.label}
          </span>
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
              includeDeleted: listQuery.includeDeleted,
            });
          }}
          disabled={loading && !listQuery.name}
        >
          {uiText.definitionsPage.search.clear}
        </button>
        <label className="text-sm">
          <span className="text-[var(--md-sys-color-on-surface-variant)]">
            {uiText.definitionsPage.sortByLabel}
          </span>
          <select
            className="mt-1 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm text-[var(--md-sys-color-on-surface)]"
            value={effectiveSortBy}
            onChange={(event) =>
              goTo({
                ...listQuery,
                pagination: { ...listQuery.pagination, offset: 0 },
                sort: { ...listQuery.sort, sortBy: event.target.value },
              })
            }
          >
            <option value="createdAt">{uiText.definitionsPage.sortByCreatedAt}</option>
            <option value="name">{uiText.definitionsPage.sortByName}</option>
          </select>
        </label>
        <label className="text-sm">
          <span className="text-[var(--md-sys-color-on-surface-variant)]">
            {uiText.definitionsPage.sortOrderLabel}
          </span>
          <select
            className="mt-1 rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-2 text-sm text-[var(--md-sys-color-on-surface)]"
            value={effectiveSortOrder}
            onChange={(event) =>
              goTo({
                ...listQuery,
                pagination: { ...listQuery.pagination, offset: 0 },
                sort: { ...listQuery.sort, sortOrder: event.target.value as SortOrder },
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
                includeDeleted: event.target.checked || undefined,
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

      {loading && <PageState state="loading" message={uiText.definitionsPage.loading} />}

      {empty && <PageState state="empty" message={uiText.definitionsPage.emptyNoMatch} />}

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
                <li
                  key={definition.displayId}
                  className="flex flex-wrap items-center justify-between gap-3 px-4 py-3"
                >
                  <div className="min-w-0 flex-1">
                    <p
                      className="flex flex-wrap items-center gap-2 truncate font-medium text-[var(--md-sys-color-on-surface)]"
                      title={definition.name}
                    >
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
                        uiText.definitionsPage.createdAt(
                          formatDateTimeLocalized(definition.createdAt, dateTimeLocale),
                        ),
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
                          onClick={() =>
                            router.push(`/definitions/${encodeURIComponent(definition.displayId)}`)
                          }
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
          <div className="mt-2 flex justify-end">{paginationNav}</div>
        </section>
      )}

      {!loading && items === null && !toast && (
        <PageState
          state="error"
          message={uiText.definitionsPage.error}
          onRetry={() => void loadDefinitions()}
        />
      )}
    </PageShell>
  );
}
