"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { Toast } from "../../components/Toast";
import { apiGet } from "../../lib/api";
import { toToastError, type ToastState } from "../../lib/errors";
import type { DefinitionDTO } from "../../lib/types";
import { uiText } from "../../lib/uiText";

function formatDateTime(iso: string): string {
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return iso;
  return parsed.toLocaleString("ja-JP", { dateStyle: "short", timeStyle: "short" });
}

type DefinitionDetailClientProps = {
  definitionId: string;
};

/**
 * Definition 詳細（API のメタ情報、Workflow 一覧・編集・実行開始への導線）を表示する。
 */
export function DefinitionDetailClient({ definitionId }: Readonly<DefinitionDetailClientProps>) {
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
        <h1 className="text-xl font-semibold text-zinc-900">{uiText.definitionDetail.title}</h1>
        <p className="text-sm text-zinc-600">
          {uiText.definitionDetail.urlPrefix} <span className="font-mono">{definitionId}</span>
        </p>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && (
        <output className="block text-sm text-zinc-500" aria-live="polite">
          {uiText.actions.loading}
        </output>
      )}

      {!loading && !row && toast && <p className="text-sm text-zinc-600">{uiText.definitionDetail.errorFetchFailed}</p>}

      {!loading && row && (
        <section
          className="rounded-lg border border-zinc-200 bg-white p-4 text-sm text-zinc-800 shadow-sm"
          aria-label={uiText.definitionDetail.ariaMeta}
        >
          <dl className="grid grid-cols-[minmax(7rem,auto)_1fr] gap-x-3 gap-y-2">
            <dt className="text-zinc-500">{uiText.definitionDetail.meta.name}</dt>
            <dd className="font-medium">{row.name}</dd>
            <dt className="text-zinc-500">{uiText.labels.displayId}</dt>
            <dd className="font-mono break-all">{row.displayId}</dd>
            <dt className="text-zinc-500">resourceId</dt>
            <dd className="font-mono break-all">{row.resourceId}</dd>
            <dt className="text-zinc-500">{uiText.definitionDetail.meta.createdAt}</dt>
            <dd>{formatDateTime(row.createdAt)}</dd>
          </dl>
        </section>
      )}

      <section className="rounded-lg border border-amber-100 bg-amber-50/80 p-4 text-sm text-amber-950">
        <h2 className="font-medium text-amber-950">{uiText.definitionDetail.relatedWorkflows.title}</h2>
        <p className="mt-1 text-amber-900/90">
          {uiText.definitionDetail.relatedWorkflows.description}
        </p>
        <p className="mt-2">
          <Link
            className="text-blue-800 underline hover:text-blue-950"
            href={`/workflows?definitionId=${encodeURIComponent(definitionId)}`}
          >
            {uiText.definitionDetail.relatedWorkflows.openList}
          </Link>
        </p>
      </section>

      <section className="space-y-2 text-sm text-zinc-800">
        <h2 className="font-medium text-zinc-900">{uiText.definitionDetail.actions.title}</h2>
        <ul className="list-inside list-disc space-y-1.5 text-zinc-700">
          <li>
            <Link className="text-blue-700 underline hover:text-blue-900" href={`/definitions/${encodeURIComponent(definitionId)}/edit`}>
              {uiText.definitionDetail.actions.edit}
            </Link>
          </li>
          <li>
            <Link className="text-blue-700 underline hover:text-blue-900" href={`/definitions/${encodeURIComponent(definitionId)}/run`}>
              {uiText.definitionDetail.actions.run}
            </Link>
          </li>
        </ul>
      </section>

      <nav className="flex flex-wrap gap-3 text-sm">
        <Link className="text-blue-700 underline hover:text-blue-900" href="/definitions">
          {uiText.definitionDetail.nav.backToDefinitions}
        </Link>
        <Link className="text-blue-700 underline hover:text-blue-900" href="/dashboard">
          {uiText.navigation.dashboard}
        </Link>
      </nav>
    </div>
  );
}
