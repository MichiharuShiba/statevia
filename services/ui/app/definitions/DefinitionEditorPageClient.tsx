"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { DefinitionGraphEditor } from "../components/editor/DefinitionGraphEditor";
import { YamlCodeEditor } from "../components/editor/YamlCodeEditor";
import { ActionLinkGroup } from "../components/layout/ActionLinkGroup";
import { NAVIGATION_BUTTON_CLASS } from "../components/layout/navigationButtonClass";
import { PageShell } from "../components/layout/PageShell";
import { PageState } from "../components/layout/PageState";
import { Toast } from "../components/Toast";
import { apiGet, apiPost } from "../lib/api";
import { parseDefinitionYaml, type ParseDefinitionYamlMessageOptions } from "../lib/definition-editor/parseDefinitionYaml";
import { serializeDefinitionYaml } from "../lib/definition-editor/serializeDefinitionYaml";
import type { DefinitionGraphDocument } from "../lib/definition-editor/types";
import { validateGraphDocument, type ValidateGraphDocumentMessageOptions } from "../lib/definition-editor/validateGraphDocument";
import { defaultDefinitionYaml } from "../lib/defaultDefinitionYaml";
import { toToastError, type ToastState } from "../lib/errors";
import type { DefinitionDTO, DefinitionSchemaResponse } from "../lib/types";
import { useUiText } from "../lib/uiTextContext";
import { getUtf8ByteLength, matchesPattern } from "../lib/validation/primitives";
import { DEFINITION_NAME_PATTERN, DEFINITION_YAML_MAX_BYTES } from "../lib/validation/formRules";

type DefinitionEditorPageClientProps = {
  definitionId?: string;
};

type EditorMode = "yaml" | "graph";

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
  const graphValidationMessageOptions = useMemo<ValidateGraphDocumentMessageOptions>(
    () => ({
      nodesRequired: uiText.definitionEditor.graph.nodesRequired,
      nodeIdRequired: uiText.definitionEditor.graph.nodeIdRequired,
      duplicateNodeId: uiText.definitionEditor.graph.duplicateNodeId,
      startCountInvalid: uiText.definitionEditor.graph.startCountInvalid,
      endCountInvalid: uiText.definitionEditor.graph.endCountInvalid,
      startRequiresTransition: uiText.definitionEditor.graph.startRequiresTransition,
      actionRequired: uiText.definitionEditor.graph.actionRequired,
      actionRequiresTransition: uiText.definitionEditor.graph.actionRequiresTransition,
      waitEventRequired: uiText.definitionEditor.graph.waitEventRequired,
      waitRequiresTransition: uiText.definitionEditor.graph.waitRequiresTransition,
      forkBranchesRequired: uiText.definitionEditor.graph.forkBranchesRequired,
      joinRequiresTransition: uiText.definitionEditor.graph.joinRequiresTransition,
      joinModeInvalid: uiText.definitionEditor.graph.joinModeInvalid,
      endCannotHaveTransition: uiText.definitionEditor.graph.endCannotHaveTransition,
      edgeToRequired: uiText.definitionEditor.graph.edgeToRequired,
      edgeWhenPathRequired: uiText.definitionEditor.graph.edgeWhenPathRequired,
      edgeWhenOpRequired: uiText.definitionEditor.graph.edgeWhenOpRequired,
      edgeWhenValueRequired: uiText.definitionEditor.graph.edgeWhenValueRequired,
      edgeWhenValueInInvalid: uiText.definitionEditor.graph.edgeWhenValueInInvalid,
      edgeWhenValueBetweenInvalid: uiText.definitionEditor.graph.edgeWhenValueBetweenInvalid,
      edgeDefaultMultiple: uiText.definitionEditor.graph.edgeDefaultMultiple,
      selfReferenceEdge: uiText.definitionEditor.graph.selfReferenceEdge,
      missingTargetNode: uiText.definitionEditor.graph.missingTargetNode
    }),
    [uiText.definitionEditor.graph]
  );
  const router = useRouter();
  const parseYamlMessageOptions = useMemo<ParseDefinitionYamlMessageOptions>(
    () => ({
      rootObjectRequired: uiText.definitionEditor.graph.rootObjectRequired,
      nodesArrayRequired: uiText.definitionEditor.graph.nodesArrayRequired
    }),
    [uiText.definitionEditor.graph.nodesArrayRequired, uiText.definitionEditor.graph.rootObjectRequired]
  );
  const isCreateMode = !definitionId;
  // 編集対象のメタ情報（名前など）ロード中フラグ。新規作成では不要。
  const [loadingMeta, setLoadingMeta] = useState(!isCreateMode);
  // フォーム本体の状態（name/yaml）。
  const [definitionName, setDefinitionName] = useState("");
  const [yaml, setYaml] = useState(defaultDefinitionYaml);
  const [editorMode, setEditorMode] = useState<EditorMode>("yaml");
  const [graphDocument, setGraphDocument] = useState<DefinitionGraphDocument | null>(null);
  const [graphValidationMessages, setGraphValidationMessages] = useState<string[]>([]);
  const [yamlParseMessages, setYamlParseMessages] = useState<string[]>([]);
  const [initialSnapshot, setInitialSnapshot] = useState<{ name: string; yaml: string } | null>(null);
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
  const yamlRef = useRef(yaml);
  const graphDocumentRef = useRef(graphDocument);
  // 空文字 or lint エラーを保存禁止条件として統一する。
  const hasYamlError = useMemo(
    () => !yaml.trim() || yamlHasLintErrors || yamlParseMessages.length > 0 || graphValidationMessages.length > 0,
    [graphValidationMessages.length, yaml, yamlHasLintErrors, yamlParseMessages.length]
  );
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
    if (yamlParseMessages.length > 0) {
      return yamlParseMessages;
    }
    return yamlDiagnostics;
  }, [apiDiagnostics, apiYamlMessages, yamlDiagnostics, yamlParseMessages]);
  const graphHintMessages = useMemo(() => {
    if (graphValidationMessages.length > 0) {
      return graphValidationMessages;
    }
    return yamlParseMessages;
  }, [graphValidationMessages, yamlParseMessages]);
  const canResetToInitial = useMemo(() => {
    if (!initialSnapshot) {
      return false;
    }
    return initialSnapshot.name !== definitionName || initialSnapshot.yaml !== yaml;
  }, [definitionName, initialSnapshot, yaml]);
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
      const sourceYaml = typeof row.yaml === "string" && row.yaml.trim().length > 0 ? row.yaml : defaultDefinitionYaml;
      setYaml(sourceYaml);
      yamlRef.current = sourceYaml;
      const parsed = parseDefinitionYaml(sourceYaml, parseYamlMessageOptions);
      if (parsed.document) {
        const validated = validateGraphDocument(parsed.document, graphValidationMessageOptions);
        setYamlParseMessages(validated.isValid ? [] : validated.messages);
        setGraphValidationMessages(validated.messages);
        if (validated.isValid) {
          setGraphDocument(parsed.document);
        }
      } else {
        setYamlParseMessages(parsed.diagnostics);
      }
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setLoadingMeta(false);
    }
  }, [definitionId, graphValidationMessageOptions, parseYamlMessageOptions]);

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

  useEffect(() => {
    yamlRef.current = yaml;
  }, [yaml]);

  useEffect(() => {
    graphDocumentRef.current = graphDocument;
  }, [graphDocument]);

  const parseYamlImmediately = useCallback(
    (yamlText: string) => {
      const parsed = parseDefinitionYaml(yamlText, parseYamlMessageOptions);
      if (!parsed.document) {
        setYamlParseMessages(parsed.diagnostics);
        return false;
      }
      const validated = validateGraphDocument(parsed.document, graphValidationMessageOptions);
      setYamlParseMessages(validated.isValid ? [] : validated.messages);
      setGraphValidationMessages(validated.messages);
      if (validated.isValid) {
        setGraphDocument(parsed.document);
      }
      return validated.isValid;
    },
    [graphValidationMessageOptions, parseYamlMessageOptions]
  );

  // グラフタブではドキュメントが親の yaml より先行することがある（ID 編集中の無効な中間状態など）。
  // デバウンスした yaml 再パースで graphDocument を上書きすると編集中の状態が消えるため、YAML タブのときだけ同期する。
  useEffect(() => {
    if (editorMode === "graph") {
      return;
    }
    const timer = setTimeout(() => {
      parseYamlImmediately(yaml);
    }, 300);
    return () => clearTimeout(timer);
  }, [editorMode, parseYamlImmediately, yaml]);

  useEffect(() => {
    if (initialSnapshot || loadingMeta) {
      return;
    }
    setInitialSnapshot({
      name: definitionName,
      yaml
    });
  }, [definitionName, initialSnapshot, loadingMeta, yaml]);

  const handleGraphDocumentChange = useCallback((nextDocument: DefinitionGraphDocument) => {
    graphDocumentRef.current = nextDocument;
    const validated = validateGraphDocument(nextDocument, graphValidationMessageOptions);
    setGraphValidationMessages(validated.messages);
    setYamlParseMessages(validated.isValid ? [] : validated.messages);
    setGraphDocument(nextDocument);
    if (!validated.isValid) {
      return;
    }
    const serialized = serializeDefinitionYaml(nextDocument);
    yamlRef.current = serialized;
    setYaml(serialized);
  }, [graphValidationMessageOptions]);

  const handleYamlChange = useCallback((nextYaml: string) => {
    yamlRef.current = nextYaml;
    setYaml(nextYaml);
  }, []);

  /**
   * 保存処理（UI事前検証 -> API最終検証）。
   * UI側で弾けるものは先に弾き、API 422 は診断として再表示する。
   */
  const handleSave = useCallback(async () => {
    const name = definitionName.trim();
    const latestYaml = yamlRef.current;
    const latestGraphDocument = graphDocumentRef.current;
    let yamlForSave = latestYaml;
    if (editorMode === "graph" && latestGraphDocument) {
      yamlForSave = serializeDefinitionYaml(latestGraphDocument);
    } else {
      // YAML モードでも保存直前に最新テキストを再パースし、送信内容を確定する。
      const parsedForSave = parseDefinitionYaml(latestYaml, parseYamlMessageOptions);
      if (parsedForSave.document) {
        yamlForSave = serializeDefinitionYaml(parsedForSave.document);
      }
    }
    const yamlText = yamlForSave.trim();
    if (!name) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.nameRequired
      });
      return;
    }
    if (!yamlText) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.yamlRequired
      });
      return;
    }
    if (!matchesPattern(name, DEFINITION_NAME_PATTERN)) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.nameInvalidFormat
      });
      return;
    }
    const yamlBytes = getUtf8ByteLength(yamlText);
    if (yamlBytes > DEFINITION_YAML_MAX_BYTES) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.yamlTooLarge
      });
      return;
    }
    if (hasYamlError) {
      setToast({
        tone: "error",
        message: uiText.definitionEditor.validation.yamlLintInvalid
      });
      return;
    }

    setSaving(true);
    setToast(null);
    setSavedDefinition(null);
    setApiValidationDetails([]);
    // 直前の API 診断は保存試行ごとにクリアする。
    setApiDiagnostics([]);
    try {
      // 保存 payload は直前の Graph 編集内容を優先して確定する。
      yamlRef.current = yamlForSave;
      setYaml(yamlForSave);
      const created = await apiPost<DefinitionDTO>("/definitions", { name, yaml: yamlForSave });
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
  }, [definitionName, editorMode, hasYamlError, parseYamlMessageOptions, uiText.definitionEditor.validation.nameInvalidFormat, uiText.definitionEditor.validation.nameRequired, uiText.definitionEditor.validation.yamlLintInvalid, uiText.definitionEditor.validation.yamlRequired, uiText.definitionEditor.validation.yamlTooLarge, uiText.definitionEditor.toasts, uiText.labels.displayId]);

  return (
    <PageShell
      title={uiText.labels.definitionEditor}
      description={
        isCreateMode
          ? uiText.definitionEditor.descriptionCreating
          : uiText.definitionEditor.descriptionEditingTarget(definitionId)
      }
      primaryActions={<ActionLinkGroup links={actionLinks} />}
      className="max-w-[1600px]"
    >

      <Toast toast={toast} onClose={() => setToast(null)} />

      {loadingMeta && (
        <PageState state="loading" message={uiText.definitionEditor.loadingMeta} />
      )}

      <section className="space-y-3 rounded-lg border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4 shadow-sm">
        <label className="block text-sm">
          <span className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionEditor.labels.name}</span>
          <input
            className="mt-1 w-full rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-2 py-1.5 text-sm text-[var(--md-sys-color-on-surface)]"
            value={definitionName}
            onChange={(event) => setDefinitionName(event.target.value)}
            autoComplete="off"
          />
        </label>
        {apiNameMessages.length > 0 && (
          <p className="text-xs text-rose-600">{apiNameMessages[0]}</p>
        )}

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className={`rounded border px-3 py-1 text-xs ${editorMode === "yaml" ? "border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] text-[var(--brand-cta-fg)]" : "border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] text-[var(--md-sys-color-on-surface)]"}`}
            onClick={() => setEditorMode("yaml")}
          >
            {uiText.definitionEditor.actions.switchToYaml}
          </button>
          <button
            type="button"
            className={`rounded border px-3 py-1 text-xs ${editorMode === "graph" ? "border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] text-[var(--brand-cta-fg)]" : "border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] text-[var(--md-sys-color-on-surface)]"}`}
            onClick={() => {
              parseYamlImmediately(yaml);
              setEditorMode("graph");
            }}
          >
            {uiText.definitionEditor.actions.switchToGraph}
          </button>
        </div>

        {editorMode === "yaml" ? (
          <label className="block text-sm">
            <span className="text-[var(--md-sys-color-on-surface-variant)]">{uiText.definitionEditor.labels.yaml}</span>
            <YamlCodeEditor
              value={yaml}
              onChange={handleYamlChange}
              completionKeywords={completionKeywords}
              onLintChange={setYamlHasLintErrors}
              onDiagnosticsChange={setYamlDiagnostics}
            />
          </label>
        ) : (
          <>
            {yamlParseMessages.length > 0 && (
              <p className="text-xs text-amber-700">{uiText.definitionEditor.graph.parseFailed}</p>
            )}
            <DefinitionGraphEditor
              document={graphDocument}
              onDocumentChange={handleGraphDocumentChange}
              validationMessages={graphHintMessages}
              labels={uiText.definitionEditor.graph}
            />
          </>
        )}
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
            className="w-full rounded border-2 border-[var(--brand-cta-border)] bg-[var(--brand-cta-bg)] px-3 py-1.5 text-sm text-[var(--brand-cta-fg)] hover:bg-[var(--brand-cta-bg-hover)] disabled:opacity-50 sm:w-auto"
            onClick={() => void handleSave()}
            disabled={saving || hasYamlError}
          >
            {saving ? uiText.definitionEditor.actions.saving : uiText.definitionEditor.actions.saveWithApiHint}
          </button>
          <button
            type="button"
            className="w-full rounded border border-[var(--md-sys-color-outline-variant)] bg-[var(--md-sys-color-surface-container)] px-3 py-1.5 text-sm text-[var(--md-sys-color-on-surface)] hover:bg-[var(--md-sys-color-surface-container-high)] sm:ml-auto sm:w-auto"
            onClick={() => {
              if (!initialSnapshot) {
                return;
              }
              setDefinitionName(initialSnapshot.name);
              setYaml(initialSnapshot.yaml);
              parseYamlImmediately(initialSnapshot.yaml);
            }}
            disabled={saving || !canResetToInitial}
          >
            {uiText.definitionEditor.actions.resetTemplate}
          </button>
        </div>

        <p className="text-xs text-[var(--md-sys-color-on-surface-variant)]">
          {uiText.definitionEditor.noteMvp}
        </p>
      </section>

      {savedDefinition && (
        <section className="space-y-2 rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-950">
          <p className="font-medium">{uiText.definitionEditor.saved.complete(savedDefinition.displayId)}</p>
          <div className="flex flex-wrap gap-3">
            <button
              type="button"
              className={NAVIGATION_BUTTON_CLASS}
              onClick={() => router.push(`/definitions/${encodeURIComponent(savedDefinition.displayId)}`)}
            >
              {uiText.definitionEditor.saved.openNewDetail}
            </button>
            <button
              type="button"
              className={NAVIGATION_BUTTON_CLASS}
              onClick={() => router.push(`/definitions/${encodeURIComponent(savedDefinition.displayId)}/run`)}
            >
              {uiText.definitionEditor.saved.runWithThisDefinition}
            </button>
          </div>
        </section>
      )}
    </PageShell>
  );
}
