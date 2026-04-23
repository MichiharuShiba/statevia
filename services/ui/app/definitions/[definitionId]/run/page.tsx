"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { Toast } from "../../../components/Toast";
import { apiPost } from "../../../lib/api";
import { toToastError, type ToastState } from "../../../lib/errors";
import type { WorkflowDTO } from "../../../lib/types";

/**
 * Definition 起点で新規ワークフローを開始する。
 * 開始成功時は `/workflows/[workflowId]/run` へ遷移する。
 */
export default function DefinitionRunStartPage() {
  const params = useParams();
  const router = useRouter();
  const definitionId = useMemo(() => {
    const raw = params.definitionId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.definitionId]);

  const [inputJson, setInputJson] = useState("");
  const [toast, setToast] = useState<ToastState | null>(null);
  const [starting, setStarting] = useState(false);

  const handleStart = async () => {
    const id = definitionId.trim();
    if (!id) {
      setToast({ tone: "error", message: "definitionId が指定されていません。" });
      return;
    }

    const body: { definitionId: string; input?: unknown } = { definitionId: id };
    if (inputJson.trim()) {
      try {
        body.input = JSON.parse(inputJson) as unknown;
      } catch {
        setToast({ tone: "error", message: "workflow input の JSON が不正です。" });
        return;
      }
    }

    setStarting(true);
    setToast(null);
    try {
      const created = await apiPost<WorkflowDTO>("/workflows", body);
      setToast({ tone: "success", message: `ワークフローを開始しました: ${created.displayId}` });
      router.push(`/workflows/${encodeURIComponent(created.displayId)}/run`);
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setStarting(false);
    }
  };

  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-5 p-6">
      <header className="space-y-1">
        <h1 className="text-xl font-semibold text-zinc-900">定義起点で実行</h1>
        <p className="text-sm text-zinc-600">
          definitionId: <span className="font-mono break-all">{definitionId || "（未指定）"}</span>
        </p>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      <section className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
        <label className="block text-sm">
          <span className="text-zinc-600">workflow input（任意・JSON）</span>
          <textarea
            className="mt-1 h-28 w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-xs"
            value={inputJson}
            onChange={(event) => setInputJson(event.target.value)}
            placeholder='例: {"orderId":"123"}'
            spellCheck={false}
          />
        </label>
        <button
          type="button"
          className="rounded bg-blue-700 px-3 py-1.5 text-sm text-white hover:bg-blue-600 disabled:opacity-50"
          onClick={() => void handleStart()}
          disabled={starting || !definitionId.trim()}
        >
          {starting ? "開始中..." : "ワークフロー開始"}
        </button>
        <p className="text-xs text-zinc-500">
          開始後は Run 画面（<code>/workflows/[workflowId]/run</code>）へ自動遷移します。
        </p>
      </section>

      <nav className="flex flex-wrap gap-3 text-sm">
        <Link className="text-blue-700 underline hover:text-blue-900" href={`/definitions/${encodeURIComponent(definitionId)}`}>
          定義の詳細へ戻る
        </Link>
        <Link className="text-blue-700 underline hover:text-blue-900" href="/workflows">
          Workflow 一覧
        </Link>
      </nav>
    </main>
  );
}
