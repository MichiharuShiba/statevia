"use client";

import Link from "next/link";
import { useCallback, useState } from "react";
import { Toast } from "../components/Toast";
import { apiPost } from "../lib/api";
import { toToastError, type ToastState } from "../lib/errors";
import type { WorkflowDTO } from "../lib/types";
import { defaultPlaygroundYaml } from "./defaultYaml";

type DefinitionCreateResponse = {
  displayId: string;
  resourceId: string;
  name: string;
  createdAt: string;
};

export default function PlaygroundPage() {
  const [definitionName, setDefinitionName] = useState("playground-def");
  const [yaml, setYaml] = useState(defaultPlaygroundYaml);
  const [definitionId, setDefinitionId] = useState("");
  const [inputJson, setInputJson] = useState("");
  const [lastDefinition, setLastDefinition] = useState<DefinitionCreateResponse | null>(null);
  const [lastWorkflow, setLastWorkflow] = useState<WorkflowDTO | null>(null);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [busy, setBusy] = useState<"register" | "start" | null>(null);

  const registerDefinition = useCallback(async () => {
    setBusy("register");
    setToast(null);
    try {
      const res = await apiPost<DefinitionCreateResponse>("/definitions", {
        name: definitionName.trim(),
        yaml
      });
      setLastDefinition(res);
      setDefinitionId(res.displayId);
      setToast({ tone: "success", message: `定義を登録しました（displayId: ${res.displayId}）` });
    } catch (e) {
      setToast(toToastError(e));
      setLastDefinition(null);
    } finally {
      setBusy(null);
    }
  }, [definitionName, yaml]);

  const startWorkflow = useCallback(async () => {
    const id = definitionId.trim();
    if (!id) {
      setToast({ tone: "error", message: "definitionId を入力するか、先に定義を登録してください。" });
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
    setBusy("start");
    setToast(null);
    try {
      const res = await apiPost<WorkflowDTO>("/workflows", body);
      setLastWorkflow(res);
      setToast({ tone: "success", message: `ワークフローを開始しました（displayId: ${res.displayId}）` });
    } catch (e) {
      setToast(toToastError(e));
      setLastWorkflow(null);
    } finally {
      setBusy(null);
    }
  }, [definitionId, inputJson]);

  return (
    <div className="space-y-6 py-4">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-xl font-bold">Playground</h1>
          <p className="text-sm text-zinc-600">YAML で定義を登録し、Core-API 上でワークフローを開始します。</p>
        </div>
        <Link className="text-sm text-blue-700 hover:underline" href="/">
          ← Execution UI
        </Link>
      </header>

      <Toast toast={toast} onClose={() => setToast(null)} />

      <div className="grid gap-6 lg:grid-cols-2">
        <section className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-zinc-800">1. 定義（YAML）</h2>
          <label className="block text-sm">
            <span className="text-zinc-600">定義名（name）</span>
            <input
              className="mt-1 w-full rounded border border-zinc-300 px-2 py-1.5 text-sm"
              value={definitionName}
              onChange={(e) => setDefinitionName(e.target.value)}
              autoComplete="off"
            />
          </label>
          <label className="block text-sm">
            <span className="text-zinc-600">YAML</span>
            <textarea
              className="mt-1 h-64 w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-xs"
              value={yaml}
              onChange={(e) => setYaml(e.target.value)}
              spellCheck={false}
            />
          </label>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              className="rounded bg-zinc-900 px-3 py-1.5 text-sm text-white hover:bg-zinc-800 disabled:opacity-50"
              onClick={() => setYaml(defaultPlaygroundYaml)}
              disabled={!!busy}
            >
              テンプレートに戻す
            </button>
            <button
              type="button"
              className="rounded bg-emerald-700 px-3 py-1.5 text-sm text-white hover:bg-emerald-600 disabled:opacity-50"
              onClick={() => void registerDefinition()}
              disabled={!!busy || !definitionName.trim()}
            >
              {busy === "register" ? "登録中…" : "定義を登録"}
            </button>
          </div>
          {lastDefinition && (
            <dl className="rounded bg-zinc-50 p-3 text-xs text-zinc-800">
              <div className="grid grid-cols-[auto_1fr] gap-x-2 gap-y-1">
                <dt className="text-zinc-500">displayId</dt>
                <dd className="font-mono">{lastDefinition.displayId}</dd>
                <dt className="text-zinc-500">resourceId</dt>
                <dd className="font-mono">{String(lastDefinition.resourceId)}</dd>
                <dt className="text-zinc-500">createdAt</dt>
                <dd>{lastDefinition.createdAt}</dd>
              </div>
            </dl>
          )}
        </section>

        <section className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
          <h2 className="font-semibold text-zinc-800">2. 実行開始</h2>
          <label className="block text-sm">
            <span className="text-zinc-600">definitionId（displayId または UUID）</span>
            <input
              className="mt-1 w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-sm"
              value={definitionId}
              onChange={(e) => setDefinitionId(e.target.value)}
              placeholder="登録後に自動入力されます"
              autoComplete="off"
            />
          </label>
          <label className="block text-sm">
            <span className="text-zinc-600">workflow input（任意・JSON）</span>
            <textarea
              className="mt-1 h-24 w-full rounded border border-zinc-300 px-2 py-1.5 font-mono text-xs"
              value={inputJson}
              onChange={(e) => setInputJson(e.target.value)}
              placeholder='例: {"orderId":"123"}'
              spellCheck={false}
            />
          </label>
          <button
            type="button"
            className="rounded bg-blue-700 px-3 py-1.5 text-sm text-white hover:bg-blue-600 disabled:opacity-50"
            onClick={() => void startWorkflow()}
            disabled={!!busy}
          >
            {busy === "start" ? "開始中…" : "ワークフロー開始"}
          </button>
          {lastWorkflow && (
            <dl className="rounded bg-zinc-50 p-3 text-xs text-zinc-800">
              <div className="grid grid-cols-[auto_1fr] gap-x-2 gap-y-1">
                <dt className="text-zinc-500">displayId</dt>
                <dd className="font-mono">{lastWorkflow.displayId}</dd>
                <dt className="text-zinc-500">resourceId</dt>
                <dd className="font-mono">{lastWorkflow.resourceId}</dd>
                <dt className="text-zinc-500">status</dt>
                <dd>{lastWorkflow.status}</dd>
                <dt className="text-zinc-500">startedAt</dt>
                <dd>{lastWorkflow.startedAt}</dd>
              </div>
              <p className="mt-2 text-zinc-600">
                実行 UI で詳細を見る場合は{" "}
                <Link href="/" className="text-blue-700 underline">
                  Execution UI
                </Link>{" "}
                の実行 ID に <span className="font-mono">{lastWorkflow.displayId}</span> を入力してください。
              </p>
            </dl>
          )}
        </section>
      </div>
    </div>
  );
}
