"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { Toast } from "../../../components/Toast";
import { apiPost } from "../../../lib/api";
import { toToastError, type ToastState } from "../../../lib/errors";
import type { WorkflowDTO } from "../../../lib/types";
import { useUiText } from "../../../lib/uiTextContext";

/**
 * Definition 起点で新規ワークフローを開始する。
 * 開始成功時は `/workflows/[workflowId]/run` へ遷移する。
 */
export default function DefinitionRunStartPage() {
  const uiText = useUiText();
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
      setToast({ tone: "error", message: uiText.definitionRunPage.toasts.definitionIdRequired(uiText.labels.definitionId) });
      return;
    }

    const body: { definitionId: string; input?: unknown } = { definitionId: id };
    if (inputJson.trim()) {
      try {
        body.input = JSON.parse(inputJson) as unknown;
      } catch {
        setToast({ tone: "error", message: uiText.definitionRunPage.toasts.invalidWorkflowInputJson(uiText.labels.workflowInput) });
        return;
      }
    }

    setStarting(true);
    setToast(null);
    try {
      const created = await apiPost<WorkflowDTO>("/workflows", body);
      setToast({ tone: "success", message: uiText.definitionRunPage.toasts.workflowStarted(created.displayId) });
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
        <h1 className="text-xl font-semibold text-zinc-900">{uiText.definitionRunPage.title}</h1>
        <p className="text-sm text-zinc-600">
          <span className="font-mono break-all">
            {uiText.definitionRunPage.definitionIdLine(
              uiText.labels.definitionId,
              definitionId || uiText.definitionRunPage.unspecifiedDefinitionId
            )}
          </span>
        </p>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      <section className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
        <label className="block text-sm">
          <span className="text-zinc-600">{uiText.definitionRunPage.workflowInputLabelWithHint(uiText.labels.workflowInput)}</span>
          <textarea
            className="mt-1 h-28 w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-xs"
            value={inputJson}
            onChange={(event) => setInputJson(event.target.value)}
            placeholder={uiText.definitionRunPage.inputJsonPlaceholder}
            spellCheck={false}
          />
        </label>
        <button
          type="button"
          className="rounded bg-blue-700 px-3 py-1.5 text-sm text-white hover:bg-blue-600 disabled:opacity-50"
          onClick={() => void handleStart()}
          disabled={starting || !definitionId.trim()}
        >
          {starting ? uiText.definitionRunPage.actions.starting : uiText.definitionRunPage.actions.startWorkflow}
        </button>
        <p className="text-xs text-zinc-500">
          {uiText.definitionRunPage.help.redirectAfterStart("/workflows/[workflowId]/run")}
        </p>
      </section>

      <nav className="flex flex-wrap gap-3 text-sm">
        <Link className="text-blue-700 underline hover:text-blue-900" href={`/definitions/${encodeURIComponent(definitionId)}`}>
          {uiText.definitionRunPage.nav.backToDefinitionDetail}
        </Link>
        <Link className="text-blue-700 underline hover:text-blue-900" href="/workflows">
          {uiText.lists.workflows}
        </Link>
      </nav>
    </main>
  );
}
