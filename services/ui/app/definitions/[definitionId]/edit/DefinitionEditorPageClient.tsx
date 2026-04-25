"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ActionLinkGroup } from "../../../components/layout/ActionLinkGroup";
import { PageShell } from "../../../components/layout/PageShell";
import { PageState } from "../../../components/layout/PageState";
import { Toast } from "../../../components/Toast";
import { apiGet, apiPost } from "../../../lib/api";
import { defaultDefinitionYaml } from "../../../lib/defaultDefinitionYaml";
import { toToastError, type ToastState } from "../../../lib/errors";
import type { DefinitionDTO } from "../../../lib/types";
import { uiText } from "../../../lib/uiText";

type DefinitionEditorPageClientProps = {
  definitionId: string;
};

/**
 * Definition 専用エディタ。
 * MVP では既存定義の YAML 取得 API が無いため、既存 name を初期値にしつつ保存は新規登録（POST /definitions）で行う。
 */
export function DefinitionEditorPageClient({ definitionId }: Readonly<DefinitionEditorPageClientProps>) {
  const [loadingMeta, setLoadingMeta] = useState(true);
  const [definitionName, setDefinitionName] = useState("");
  const [yaml, setYaml] = useState(defaultDefinitionYaml);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [saving, setSaving] = useState(false);
  const [savedDefinition, setSavedDefinition] = useState<DefinitionDTO | null>(null);
  const actionLinks = [
    { label: "定義の詳細へ戻る", href: `/definitions/${encodeURIComponent(definitionId)}`, priority: "primary" as const },
    { label: uiText.lists.definitions, href: "/definitions" }
  ];

  const loadDefinition = useCallback(async () => {
    setLoadingMeta(true);
    try {
      const row = await apiGet<DefinitionDTO>(`/definitions/${encodeURIComponent(definitionId)}`);
      setDefinitionName((current) => (current.trim() ? current : row.name));
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setLoadingMeta(false);
    }
  }, [definitionId]);

  useEffect(() => {
    void loadDefinition();
  }, [loadDefinition]);

  const handleSave = useCallback(async () => {
    const name = definitionName.trim();
    const yamlText = yaml.trim();
    if (!name) {
      setToast({ tone: "error", message: "定義名を入力してください。" });
      return;
    }
    if (!yamlText) {
      setToast({ tone: "error", message: "YAML を入力してください。" });
      return;
    }

    setSaving(true);
    setToast(null);
    setSavedDefinition(null);
    try {
      const created = await apiPost<DefinitionDTO>("/definitions", { name, yaml });
      setSavedDefinition(created);
      setToast({
        tone: "success",
        message: `定義を保存しました（${uiText.labels.displayId}: ${created.displayId}）`
      });
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSaving(false);
    }
  }, [definitionName, yaml]);

  return (
    <PageShell
      title={uiText.labels.definitionEditor}
      description={`編集対象: ${definitionId}`}
      primaryActions={<ActionLinkGroup links={actionLinks} />}
      secondaryActions={<ActionLinkGroup links={actionLinks} />}
    >

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loadingMeta && (
        <PageState state="loading" message="定義メタ情報を読み込み中..." />
      )}

      <section className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
        <label className="block text-sm">
          <span className="text-zinc-600">定義名（name）</span>
          <input
            className="mt-1 w-full rounded border border-zinc-300 px-2 py-1.5 text-sm"
            value={definitionName}
            onChange={(event) => setDefinitionName(event.target.value)}
            autoComplete="off"
          />
        </label>

        <label className="block text-sm">
          <span className="text-zinc-600">YAML</span>
          <textarea
            className="mt-1 h-[26rem] w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-xs"
            value={yaml}
            onChange={(event) => setYaml(event.target.value)}
            spellCheck={false}
          />
        </label>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="w-full rounded bg-zinc-900 px-3 py-1.5 text-sm text-white hover:bg-zinc-800 disabled:opacity-50 sm:w-auto"
            onClick={() => void handleSave()}
            disabled={saving}
          >
            {saving ? "保存中..." : "保存（POST /definitions）"}
          </button>
          <button
            type="button"
            className="w-full rounded border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-800 hover:bg-zinc-50 sm:w-auto"
            onClick={() => setYaml(defaultDefinitionYaml)}
            disabled={saving}
          >
            テンプレートに戻す
          </button>
        </div>

        <p className="text-xs text-zinc-500">
          MVP では既存定義の更新 API がないため、保存は新規 Definition として登録されます。入力不正時は 422 をそのまま表示します。
        </p>
      </section>

      {savedDefinition && (
        <section className="space-y-2 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-950">
          <p className="font-medium">保存完了: {savedDefinition.displayId}</p>
          <div className="flex flex-wrap gap-3">
            <Link
              className="text-blue-800 underline hover:text-blue-950"
              href={`/definitions/${encodeURIComponent(savedDefinition.displayId)}`}
            >
              新しい定義の詳細へ
            </Link>
            <Link
              className="text-blue-800 underline hover:text-blue-950"
              href={`/definitions/${encodeURIComponent(savedDefinition.displayId)}/run`}
            >
              この定義で実行開始
            </Link>
          </div>
        </section>
      )}
    </PageShell>
  );
}
