"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { Toast } from "../../components/Toast";
import { apiGet } from "../../lib/api";
import { toToastError, type ToastState } from "../../lib/errors";
import type { DefinitionDTO } from "../../lib/types";

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
        <h1 className="text-xl font-semibold text-zinc-900">Definition 詳細</h1>
        <p className="text-sm text-zinc-600">
          URL: <span className="font-mono">{definitionId}</span>
        </p>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loading && (
        <output className="block text-sm text-zinc-500" aria-live="polite">
          読み込み中…
        </output>
      )}

      {!loading && !row && toast && <p className="text-sm text-zinc-600">定義を取得できませんでした。</p>}

      {!loading && row && (
        <section
          className="rounded-lg border border-zinc-200 bg-white p-4 text-sm text-zinc-800 shadow-sm"
          aria-label="定義メタ情報"
        >
          <dl className="grid grid-cols-[minmax(7rem,auto)_1fr] gap-x-3 gap-y-2">
            <dt className="text-zinc-500">名前</dt>
            <dd className="font-medium">{row.name}</dd>
            <dt className="text-zinc-500">displayId</dt>
            <dd className="font-mono break-all">{row.displayId}</dd>
            <dt className="text-zinc-500">resourceId</dt>
            <dd className="font-mono break-all">{row.resourceId}</dd>
            <dt className="text-zinc-500">登録日時</dt>
            <dd>{formatDateTime(row.createdAt)}</dd>
          </dl>
        </section>
      )}

      <section className="rounded-lg border border-amber-100 bg-amber-50/80 p-4 text-sm text-amber-950">
        <h2 className="font-medium text-amber-950">関連ワークフロー</h2>
        <p className="mt-1 text-amber-900/90">
          この定義に紐づく実行の一覧（フィルタは T5 予定）へ進みます。
        </p>
        <p className="mt-2">
          <Link
            className="text-blue-800 underline hover:text-blue-950"
            href={`/workflows?definitionId=${encodeURIComponent(definitionId)}`}
          >
            ワークフロー一覧を開く
          </Link>
        </p>
      </section>

      <section className="space-y-2 text-sm text-zinc-800">
        <h2 className="font-medium text-zinc-900">編集・実行</h2>
        <ul className="list-inside list-disc space-y-1.5 text-zinc-700">
          <li>
            <Link className="text-blue-700 underline hover:text-blue-900" href={`/definitions/${encodeURIComponent(definitionId)}/edit`}>
              定義の編集（T10: 専用 Editor へ拡張予定）
            </Link>
          </li>
          <li>
            <Link className="text-blue-700 underline hover:text-blue-900" href={`/definitions/${encodeURIComponent(definitionId)}/run`}>
              新規実行を開始（T7: 専用 Run 画面へ拡張予定）
            </Link>
          </li>
        </ul>
      </section>

      <nav className="flex flex-wrap gap-3 text-sm">
        <Link className="text-blue-700 underline hover:text-blue-900" href="/definitions">
          Definition 一覧へ戻る
        </Link>
        <Link className="text-blue-700 underline hover:text-blue-900" href="/dashboard">
          ダッシュボード
        </Link>
      </nav>
    </div>
  );
}
