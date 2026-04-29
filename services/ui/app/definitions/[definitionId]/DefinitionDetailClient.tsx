"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { NAVIGATION_BUTTON_CLASS } from "../../components/layout/navigationButtonClass";
import { Toast } from "../../components/Toast";
import { apiGet } from "../../lib/api";
import { formatDateTimeLocalized } from "../../lib/dateTime";
import { toToastError, type ToastState } from "../../lib/errors";
import { getDateTimeLocale } from "../../lib/i18n";
import type { DefinitionDTO } from "../../lib/types";
import { useI18n } from "../../lib/uiTextContext";

type DefinitionDetailClientProps = {
  definitionId: string;
};

/**
 * Definition 詳細（API のメタ情報、Workflow 一覧・編集・実行開始への導線）を表示する。
 */
export function DefinitionDetailClient({ definitionId }: Readonly<DefinitionDetailClientProps>) {
  const { uiText, locale } = useI18n();
  const router = useRouter();
  const dateTimeLocale = getDateTimeLocale(locale);
  const [row, setRow] = useState<DefinitionDTO | null>(null);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<ToastState | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const path = `/definitions/${encodeURIComponent(definitionId)}`;
      const d = await apiGet<DefinitionDTO>(path);
      setRow(d);
    } catch (e) {
      setToast(toToastError(e));
      setRow(null);
    } finally {
      setLoading(false);
    }
  }, [definitionId]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-5 p-6">
      <header className="space-y-1">
        <h1 className="text-xl font-semibold text-[var(--md-sys-color-on-surface)]">{uiText.definitionDetail.title}</h1>
        <p className="text-sm text-[var(--md-sys-color-on-surface-variant)]">
          {uiText.definitionDetail.urlPrefix} <span className="font-mono">{definitionId}</span>
        </p>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && (
        <output className="block text-sm text-[var(--md-sys-color-on-surface-variant)]" aria-live="polite">
          {uiText.actions.loading}
        </output>
      )}

      {!loading && !row && toast && <p className="text-sm text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionDetail.errorFetchFailed}</p>}

      {!loading && row && (
        <section
          className="rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 text-sm text-[var(--md-sys-color-on-surface)] shadow-sm"
          aria-label={uiText.definitionDetail.ariaMeta}
        >
          <dl className="grid grid-cols-[minmax(7rem,auto)_1fr] gap-x-3 gap-y-2">
            <dt className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionDetail.meta.name}</dt>
            <dd className="font-medium">{row.name}</dd>
            <dt className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.labels.displayId}</dt>
            <dd className="font-mono break-all">{row.displayId}</dd>
            <dt className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.labels.resourceId}</dt>
            <dd className="font-mono break-all">{row.resourceId}</dd>
            <dt className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionDetail.meta.createdAt}</dt>
            <dd>{formatDateTimeLocalized(row.createdAt, dateTimeLocale)}</dd>
          </dl>
        </section>
      )}

      <section className="rounded-lg border border-amber-100 bg-amber-50/80 p-4 text-sm text-amber-950">
        <h2 className="font-medium text-amber-950">{uiText.definitionDetail.relatedWorkflows.title}</h2>
        <p className="mt-1 text-amber-900/90">
          {uiText.definitionDetail.relatedWorkflows.description}
        </p>
        <p className="mt-2">
          <button
            type="button"
            className={NAVIGATION_BUTTON_CLASS}
            onClick={() => router.push(`/workflows?definitionId=${encodeURIComponent(definitionId)}`)}
          >
            {uiText.definitionDetail.relatedWorkflows.openList}
          </button>
        </p>
      </section>

      <section className="space-y-2 text-sm text-[var(--md-sys-color-on-surface)]">
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            className={NAVIGATION_BUTTON_CLASS}
            onClick={() => router.push(`/definitions/${encodeURIComponent(definitionId)}/edit`)}
          >
            {uiText.definitionDetail.actions.edit}
          </button>
          <button
            type="button"
            className="rounded border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] px-3 py-1.5 text-sm font-medium text-[var(--brand-cta-fg)] hover:bg-[var(--brand-cta-bg-hover)]"
            onClick={() => router.push(`/definitions/${encodeURIComponent(definitionId)}/run`)}
          >
            {uiText.definitionDetail.actions.run}
          </button>
        </div>
      </section>
    </div>
  );
}
