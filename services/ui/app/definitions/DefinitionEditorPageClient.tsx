"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { YamlCodeEditor } from "../components/editor/YamlCodeEditor";
import { ActionLinkGroup } from "../components/layout/ActionLinkGroup";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import { apiGet, apiPost } from "../lib/api";
import { defaultDefinitionYaml } from "../lib/defaultDefinitionYaml";
import { toToastError, type ToastState } from "../lib/errors";
import type { DefinitionDTO, DefinitionSchemaResponse } from "../lib/types";
import { useUiText } from "../lib/uiTextContext";

type DefinitionEditorPageClientProps = {
  definitionId?: string;
};

type ApiErrorLike = {
  status?: number;
  error?: {
    message?: string;
    details?: unknown;
  };
};

type ValidationDetailItem = {
  field?: string;
  message?: string;
};

function isApiErrorLike(value: unknown): value is ApiErrorLike {
  return typeof value === "object" && value !== null && "error" in value;
}

function toValidationDetailItem(value: unknown): ValidationDetailItem | null {
  if (typeof value !== "object" || value === null) {
    return null;
  }
  const record = value as Record<string, unknown>;
  return {
    field: typeof record.field === "string" ? record.field : undefined,
    message: typeof record.message === "string" ? record.message : undefined
  };
}

function extractApiValidationDetails(error: unknown): ValidationDetailItem[] {
  if (!isApiErrorLike(error) || error.status !== 422) {
    return [];
  }
  const details = error.error?.details;
  if (Array.isArray(details)) {
    return details
      .map((item) => {
        if (typeof item === "string") {
          return { message: item };
        }
        return toValidationDetailItem(item);
      })
      .filter((detail): detail is ValidationDetailItem => detail !== null && typeof detail.message === "string");
  }
  return [];
}

function extractApiDiagnosticMessages(error: unknown): string[] {
  if (!isApiErrorLike(error) || error.status !== 422) {
    return [];
  }
  const details = error.error?.details;
  if (Array.isArray(details)) {
    return details
      .map((item) => {
        if (typeof item === "string") {
          return item;
        }
        const parsed = toValidationDetailItem(item);
        if (parsed?.message) {
          return parsed.field ? `[${parsed.field}] ${parsed.message}` : parsed.message;
        }
        return null;
      })
      .filter((message): message is string => typeof message === "string");
  }
  if (typeof details === "object" && details !== null) {
    return Object.values(details)
      .map((item) => (typeof item === "string" ? item : null))
      .filter((message): message is string => typeof message === "string");
  }
  return error.error?.message ? [error.error.message] : [];
}

/**
 * Definition 専用エディタ。
 * MVP では既存定義の YAML 取得 API が無いため、既存 name を初期値にしつつ保存は新規登録（POST /definitions）で行う。
 */
export function DefinitionEditorPageClient({ definitionId }: Readonly<DefinitionEditorPageClientProps>) {
  const uiText = useUiText();
  const isCreateMode = !definitionId;
  // 編集対象のメタ情報（名前など）ロード中フラグ。新規作成では不要。
  const [loadingMeta, setLoadingMeta] = useState(!isCreateMode);
  // フォーム本体の状態（name/yaml）。
  const [definitionName, setDefinitionName] = useState("");
  const [yaml, setYaml] = useState(defaultDefinitionYaml);
  // CodeMirror 側の lint 結果（true = エラーあり）。
  const [yamlHasLintErrors, setYamlHasLintErrors] = useState(false);
  // YAML パーサ由来の診断メッセージ。
  const [yamlDiagnostics, setYamlDiagnostics] = useState<string[]>([]);
  // API 422 由来の構造化診断。field ごとに表示位置を分離する。
  const [apiValidationDetails, setApiValidationDetails] = useState<ValidationDetailItem[]>([]);
  // API 422 由来の診断メッセージ（fallback 用）。
  const [apiDiagnostics, setApiDiagnostics] = useState<string[]>([]);
  const [toast, setToast] = useState<ToastState | null>(null);
  const [saving, setSaving] = useState(false);
  const [savedDefinition, setSavedDefinition] = useState<DefinitionDTO | null>(null);
  // 補完候補（API スキーマ由来 + フォールバック）。
  const [completionKeywords, setCompletionKeywords] = useState<string[]>([]);
  // 空文字 or lint エラーを保存禁止条件として統一する。
  const hasYamlError = useMemo(() => !yaml.trim() || yamlHasLintErrors, [yaml, yamlHasLintErrors]);
  // UI診断とAPI診断の二段構え。保存後の再修正では API 診断を先に見せる。
  const apiNameMessages = useMemo(
    () => apiValidationDetails.filter((detail) => detail.field === "name").map((detail) => detail.message!).filter(Boolean),
    [apiValidationDetails]
  );
  const apiYamlMessages = useMemo(
    () => apiValidationDetails.filter((detail) => detail.field === "yaml").map((detail) => detail.message!).filter(Boolean),
    [apiValidationDetails]
  );
  const hintMessages = useMemo(() => {
    if (apiYamlMessages.length > 0) {
      return apiYamlMessages;
    }
    if (apiDiagnostics.length > 0) {
      return apiDiagnostics;
    }
    return yamlDiagnostics;
  }, [apiDiagnostics, apiYamlMessages, yamlDiagnostics]);
  const actionLinks = useMemo(
    () =>
      isCreateMode
        ? [{ label: uiText.lists.definitions, href: "/definitions", priority: "primary" as const }]
        : [
            { label: uiText.definitionEditor.backToDetail, href: `/definitions/${encodeURIComponent(definitionId)}`, priority: "primary" as const },
            { label: uiText.lists.definitions, href: "/definitions" }
          ],
    [definitionId, isCreateMode, uiText.definitionEditor.backToDetail, uiText.lists.definitions]
  );

  /**
   * 編集モード時のみ、既存定義のメタ情報を読んで初期 name を補う。
   * 新規作成モードでは読み込みをスキップする。
   */
  const loadDefinition = useCallback(async () => {
    if (!definitionId) {
      setLoadingMeta(false);
      return;
    }
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

  /**
   * 補完候補の源泉となる nodes スキーマを API から取得する。
   * API が落ちていても編集を止めないため、失敗時は最小候補へフォールバックする。
   */
  const loadSchemaKeywords = useCallback(async () => {
    const fallback = ["version", "workflow", "nodes", "id", "type", "action", "next", "event", "branches", "edges"];
    try {
      const response = await apiGet<DefinitionSchemaResponse>("/definitions/schema/nodes");
      const schemaProperties = typeof response.schema.properties === "object" && response.schema.properties != null
        ? Object.keys(response.schema.properties as Record<string, unknown>)
        : [];
      setCompletionKeywords(schemaProperties.length > 0 ? schemaProperties : fallback);
    } catch {
      setCompletionKeywords(fallback);
    }
  }, []);

  useEffect(() => {
    void loadDefinition();
  }, [loadDefinition]);

  useEffect(() => {
    void loadSchemaKeywords();
  }, [loadSchemaKeywords]);

  /**
   * 保存処理（UI事前検証 -> API最終検証）。
   * UI側で弾けるものは先に弾き、API 422 は診断として再表示する。
   */
  const handleSave = useCallback(async () => {
    const name = definitionName.trim();
    const yamlText = yaml.trim();
    if (!name) {
      setToast({ tone: "error", message: uiText.definitionEditor.validation.nameRequired });
      return;
    }
    if (!yamlText) {
      setToast({ tone: "error", message: uiText.definitionEditor.validation.yamlRequired });
      return;
    }
    if (hasYamlError) {
      setToast({ tone: "error", message: uiText.definitionEditor.validation.yamlLintInvalid });
      return;
    }

    setSaving(true);
    setToast(null);
    setSavedDefinition(null);
    setApiValidationDetails([]);
    // 直前の API 診断は保存試行ごとにクリアする。
    setApiDiagnostics([]);
    try {
      const created = await apiPost<DefinitionDTO>("/definitions", { name, yaml });
      setSavedDefinition(created);
      setToast({
        tone: "success",
        message: uiText.definitionEditor.toasts.savedWithDisplayId(uiText.labels.displayId, created.displayId)
      });
    } catch (error) {
      // API が返す 422 詳細をヒント領域へ反映する。
      setApiValidationDetails(extractApiValidationDetails(error));
      setApiDiagnostics(extractApiDiagnosticMessages(error));
      setToast(toToastError(error));
    } finally {
      setSaving(false);
    }
  }, [definitionName, hasYamlError, uiText.definitionEditor.validation.nameRequired, uiText.definitionEditor.validation.yamlLintInvalid, uiText.definitionEditor.validation.yamlRequired, uiText.definitionEditor.toasts, uiText.labels.displayId, yaml]);

  return (
    <PageShell
      title={uiText.labels.definitionEditor}
      description={
        isCreateMode
          ? uiText.definitionEditor.descriptionCreating
          : uiText.definitionEditor.descriptionEditingTarget(definitionId)
      }
      primaryActions={<ActionLinkGroup links={actionLinks} />}
    >

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loadingMeta && (
        <PageState state="loading" message={uiText.definitionEditor.loadingMeta} />
      )}

      <section className="space-y-3 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
        <label className="block text-sm">
          <span className="text-zinc-600">{uiText.definitionEditor.labels.name}</span>
          <input
            className="mt-1 w-full rounded border border-zinc-300 px-2 py-1.5 text-sm"
            value={definitionName}
            onChange={(event) => setDefinitionName(event.target.value)}
            autoComplete="off"
          />
        </label>
        {apiNameMessages.length > 0 && (
          <p className="text-xs text-rose-600">{apiNameMessages[0]}</p>
        )}

        <label className="block text-sm">
          <span className="text-zinc-600">{uiText.definitionEditor.labels.yaml}</span>
          <YamlCodeEditor
            value={yaml}
            onChange={setYaml}
            completionKeywords={completionKeywords}
            onLintChange={setYamlHasLintErrors}
            onDiagnosticsChange={setYamlDiagnostics}
          />
        </label>
        {/* 保存ガード理由を画面上で明示する。 */}
        {hasYamlError && <p className="text-xs text-rose-600">{uiText.definitionEditor.validation.yamlLintInvalid}</p>}
        {/* 診断ヒント: API診断があれば優先、なければYAML診断を表示。 */}
        {hintMessages.length > 0 && (
          <section className="rounded border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
            <p className="font-medium">{uiText.definitionEditor.hints.title}</p>
            <ul className="mt-2 list-inside list-disc space-y-1">
              {hintMessages.slice(0, 8).map((message) => (
                <li key={message}>{message}</li>
              ))}
            </ul>
          </section>
        )}

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="w-full rounded bg-zinc-900 px-3 py-1.5 text-sm text-white hover:bg-zinc-800 disabled:opacity-50 sm:w-auto"
            onClick={() => void handleSave()}
            disabled={saving || hasYamlError}
          >
            {saving ? uiText.definitionEditor.actions.saving : uiText.definitionEditor.actions.saveWithApiHint}
          </button>
          <button
            type="button"
            className="w-full rounded border border-zinc-300 bg-white px-3 py-1.5 text-sm text-zinc-800 hover:bg-zinc-50 sm:ml-auto sm:w-auto"
            onClick={() => setYaml(defaultDefinitionYaml)}
            disabled={saving}
          >
            {uiText.definitionEditor.actions.resetTemplate}
          </button>
        </div>

        <p className="text-xs text-zinc-500">
          {uiText.definitionEditor.noteMvp}
        </p>
      </section>

      {savedDefinition && (
        <section className="space-y-2 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-950">
          <p className="font-medium">{uiText.definitionEditor.saved.complete(savedDefinition.displayId)}</p>
          <div className="flex flex-wrap gap-3">
            <Link
              className="text-blue-800 underline hover:text-blue-950"
              href={`/definitions/${encodeURIComponent(savedDefinition.displayId)}`}
            >
              {uiText.definitionEditor.saved.openNewDetail}
            </Link>
            <Link
              className="text-blue-800 underline hover:text-blue-950"
              href={`/definitions/${encodeURIComponent(savedDefinition.displayId)}/run`}
            >
              {uiText.definitionEditor.saved.runWithThisDefinition}
            </Link>
          </div>
        </section>
      )}
    </PageShell>
  );
}
