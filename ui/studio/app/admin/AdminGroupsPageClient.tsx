"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import { apiGet, apiPost } from "../lib/api";
import type { AdminGroupListItem } from "../lib/adminTypes";
import { toToastError, type ToastState } from "../lib/errors";
import { useAdminGroupsUiText } from "../lib/uiTextContext";

/**
 * グループ一覧・作成。
 */
export function AdminGroupsPageClient() {
  const pageUi = useAdminGroupsUiText();
  const [groupList, setGroupList] = useState<AdminGroupListItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [name, setName] = useState("");

  const loadGroups = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const list = await apiGet<AdminGroupListItem[]>("/admin/groups");
      setGroupList(list);
    } catch (error) {
      setToast(toToastError(error));
      setGroupList(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadGroups();
  }, [loadGroups]);

  async function handleCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setToast(null);
    try {
      await apiPost("/admin/groups", { name: name.trim() });
      setName("");
      await loadGroups();
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSubmitting(false);
    }
  }

  function renderListBody() {
    if (loading) return <PageState state="loading" />;
    if (groupList === null) return <PageState state="error" />;
    if (groupList.length === 0) return <PageState state="empty" />;
    return (
      <ul className="divide-y divide-[var(--md-sys-color-outline)] rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)]">
        {groupList.map((item) => (
          <li
            key={item.groupId}
            className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
          >
            <div>
              <p className="font-medium text-[var(--md-sys-color-on-surface)]">
                {item.name}
                {item.isSystem ? (
                  <span className="ml-2 rounded bg-[var(--md-sys-color-surface-container-high)] px-2 py-0.5 text-xs">
                    {pageUi.systemBadge}
                  </span>
                ) : null}
              </p>
              <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
                {pageUi.memberCount(item.memberCount)} · {pageUi.permissionCount(item.permissionCount)}
              </p>
            </div>
            <Link
              href={`/admin/groups/${item.groupId}`}
              className="rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-1.5 hover:bg-[var(--md-sys-color-surface-container)]"
            >
              {pageUi.openDetail}
            </Link>
          </li>
        ))}
      </ul>
    );
  }

  return (
    <PageShell title={pageUi.title} description={pageUi.description}>
      <form
        onSubmit={(event) => {
          void handleCreate(event);
        }}
        className="rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm"
      >
        <h2 className="mb-3 text-lg font-medium">{pageUi.createTitle}</h2>
        <label htmlFor="admin-group-name" className="mb-1 block text-sm font-medium">
          {pageUi.nameLabel}
        </label>
        <input
          id="admin-group-name"
          type="text"
          required
          value={name}
          onChange={(e) => setName(e.target.value)}
          className="w-full max-w-md rounded-lg border border-[var(--md-sys-color-outline)] px-3 py-2 text-sm"
        />
        <button
          type="submit"
          disabled={submitting}
          className="mt-4 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {submitting ? pageUi.creating : pageUi.createSubmit}
        </button>
      </form>

      {renderListBody()}

      {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}
    </PageShell>
  );
}
