"use client";

import { autocompletion, CompletionContext, type Completion } from "@codemirror/autocomplete";
import { lintGutter, linter, type Diagnostic } from "@codemirror/lint";
import { EditorState, type Extension } from "@codemirror/state";
import { EditorView, keymap } from "@codemirror/view";
import { yaml as yamlLanguage } from "@codemirror/lang-yaml";
import { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
import { useEffect, useMemo, useRef } from "react";
import { parseDocument } from "yaml";

type YamlCodeEditorProps = {
  value: string;
  onChange: (nextValue: string) => void;
  completionKeywords: readonly string[];
  onLintChange?: (hasErrors: boolean) => void;
  onDiagnosticsChange?: (messages: string[]) => void;
};

const DEFAULT_KEYWORDS = [
  "version",
  "workflow",
  "name",
  "nodes",
  "id",
  "type",
  "next",
  "action",
  "branches",
  "event",
  "mode",
  "edges",
  "to",
  "when",
  "path",
  "op",
  "value",
  "order",
  "default"
] as const;

const WORKFLOW_KEYWORDS = ["name"] as const;
const NODE_KEYWORDS = ["id", "type", "next", "action", "event", "branches", "mode", "edges"] as const;
const EDGE_KEYWORDS = ["to", "when", "order", "default"] as const;
const WHEN_KEYWORDS = ["path", "op", "value"] as const;

type CompletionScope = "root" | "workflow" | "nodesItem" | "edgeItem" | "whenObject";

/**
 * YAML パースエラーを CodeMirror の Diagnostic へ変換する。
 * - yaml パーサは line/col ベースなので、CodeMirror が要求する from/to オフセットへ変換する。
 * - to は 1 文字ぶんだけ確保し、エラー箇所に最小限の下線を引く。
 */
function createYamlDiagnostics(docText: string): Diagnostic[] {
  const parsed = parseDocument(docText, { prettyErrors: false });
  if (!parsed.errors.length) {
    return [];
  }

  return parsed.errors.map((error) => {
    const linePos = "linePos" in error && Array.isArray(error.linePos) ? error.linePos[0] : null;
    const line = linePos?.line ?? 1;
    const col = linePos?.col ?? 1;
    const from = linePos ? lineColToOffset(docText, linePos.line, linePos.col) : 0;
    const to = Math.min(docText.length, from + 1);
    const message = `YAML Lint [line ${line}, col ${col}]: ${error.message}`;

    return {
      from,
      to,
      severity: "error",
      message
    };
  });
}

/**
 * 1-origin の line/column を文字列オフセットへ変換する。
 * CodeMirror 側の座標系（0-origin offset）に合わせるためのユーティリティ。
 */
function lineColToOffset(sourceText: string, line: number, column: number): number {
  const lines = sourceText.split("\n");
  const safeLine = Math.max(1, line);
  const safeColumn = Math.max(1, column);
  let offset = 0;
  // 対象行の手前までを改行込みで積み上げる。
  for (let idx = 0; idx < safeLine - 1 && idx < lines.length; idx += 1) {
    offset += lines[idx].length + 1;
  }
  const lineText = lines[safeLine - 1] ?? "";
  // 行末を超える列が来ても安全に丸める。
  offset += Math.min(lineText.length, safeColumn - 1);
  return offset;
}

/**
 * 補完候補ソースを生成する。
 * - 既定キーワード + API 由来キーワードをユニーク化して候補にする。
 * - 入力中トークンの前方一致で絞り込む（軽量な補完戦略）。
 */
function completionSourceFactory(keywords: readonly string[]) {
  const rootKeywords = [...new Set([...DEFAULT_KEYWORDS, ...keywords])];
  const completionMap: Record<CompletionScope, Completion[]> = {
    root: rootKeywords.map((keyword) => toPropertyCompletion(keyword)),
    workflow: WORKFLOW_KEYWORDS.map((keyword) => toPropertyCompletion(keyword)),
    nodesItem: NODE_KEYWORDS.map((keyword) => toPropertyCompletion(keyword)),
    edgeItem: EDGE_KEYWORDS.map((keyword) => toPropertyCompletion(keyword)),
    whenObject: WHEN_KEYWORDS.map((keyword) => toPropertyCompletion(keyword))
  };

  return (context: CompletionContext) => {
    // カーソル直前の英数字/ハイフン/アンダースコアを補完対象トークンとして扱う。
    const token = context.matchBefore(/[A-Za-z0-9_-]*/);
    if (!token) {
      return null;
    }
    // 明示的に補完要求していない状態で空トークンなら候補を出し過ぎない。
    if (token.from === token.to && !context.explicit) {
      return null;
    }

    const scope = detectCompletionScope(context);
    const scopedCompletions = completionMap[scope];

    return {
      // 現在トークンの開始位置から補完候補を差し込む。
      from: token.from,
      // 軽量な前方一致。必要になれば fuzzy 検索へ拡張可能。
      options: scopedCompletions.filter((completion) => completion.label.startsWith(token.text))
    };
  };
}

function toPropertyCompletion(keyword: string): Completion {
  return {
    label: keyword,
    type: "property",
    apply: `${keyword}: `
  };
}

/**
 * カーソル手前の行スタックから補完スコープを推定する。
 * 厳密な AST 解析ではなく、インデントとキー名から実用的に判定する。
 */
function detectCompletionScope(context: CompletionContext): CompletionScope {
  const beforeCursor = context.state.sliceDoc(0, context.pos);
  const lines = beforeCursor.split("\n");
  type StackEntry = { key: string; indent: number };
  const stack: StackEntry[] = [];

  for (const rawLine of lines) {
    const line = rawLine.replaceAll("\t", "  ");
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }
    const indent = line.length - line.trimStart().length;
    while (stack.length > 0 && (stack.at(-1)?.indent ?? -1) >= indent) {
      stack.pop();
    }

    const key = extractYamlKey(trimmed);
    if (key) {
      stack.push({ key, indent });
    }
  }

  const keyPath = new Set(stack.map((entry) => entry.key));
  if (keyPath.has("when")) return "whenObject";
  if (keyPath.has("edges")) return "edgeItem";
  if (keyPath.has("nodes")) return "nodesItem";
  if (keyPath.has("workflow")) return "workflow";
  return "root";
}

function extractYamlKey(trimmedLine: string): string | null {
  const target = trimmedLine.startsWith("- ") ? trimmedLine.slice(2).trimStart() : trimmedLine;
  const keyPattern = /^([A-Za-z0-9_-]+)\s*:/;
  const match = keyPattern.exec(target);
  return match?.[1] ?? null;
}

/**
 * 定義 YAML 入力用の CodeMirror エディタ。
 */
export function YamlCodeEditor({
  value,
  onChange,
  completionKeywords,
  onLintChange,
  onDiagnosticsChange
}: Readonly<YamlCodeEditorProps>) {
  const hostRef = useRef<HTMLDivElement | null>(null);
  const viewRef = useRef<EditorView | null>(null);
  const completionSource = useMemo(() => completionSourceFactory(completionKeywords), [completionKeywords]);

  /**
   * Editor 拡張セット。
   * 依存が変わったときに再生成され、下の初期化 useEffect が再マウントする。
   */
  const extensions = useMemo<Extension[]>(
    () => [
      // Undo / Redo 履歴管理。
      history(),
      // 主要キーバインド（編集系 + history 操作）を有効化。
      keymap.of([...defaultKeymap, ...historyKeymap]),
      // YAML の言語サポート（トークン化・構文認識）。
      yamlLanguage(),
      // 長い行を折り返して表示（横スクロール依存を軽減）。
      EditorView.lineWrapping,
      // ドキュメント更新時に React 側 state と診断表示を同期。
      EditorView.updateListener.of((update) => {
        if (!update.docChanged) return;
        // Doc 変更時に YAML Lint を再評価し、親へエラー有無/メッセージを通知する。
        const nextText = update.state.doc.toString();
        const diagnostics = createYamlDiagnostics(nextText);
        onLintChange?.(diagnostics.length > 0);
        onDiagnosticsChange?.(diagnostics.map((diagnostic) => diagnostic.message));
        onChange(nextText);
      }),
      // 補完 UI（候補ソースは completionSourceFactory で提供）。
      autocompletion({ override: [completionSource] }),
      // 左ガターに lint マーカーを表示。
      lintGutter(),
      // YAML パース結果を lint 診断として CodeMirror に渡す。
      linter((view) => createYamlDiagnostics(view.state.doc.toString()))
    ],
    [completionSource, onChange, onDiagnosticsChange, onLintChange]
  );

  /**
   * CodeMirror インスタンスの生成/破棄。
   * React 管理外の EditorView を host 要素にマウントする。
   */
  useEffect(() => {
    const host = hostRef.current;
    if (!host) {
      return;
    }

    const state = EditorState.create({
      doc: value,
      extensions
    });

    const view = new EditorView({
      state,
      parent: host
    });
    viewRef.current = view;
    // 初期表示時点の診断も親へ渡して、保存ボタン状態を正しく同期する。
    const initialDiagnostics = createYamlDiagnostics(value);
    onLintChange?.(initialDiagnostics.length > 0);
    onDiagnosticsChange?.(initialDiagnostics.map((diagnostic) => diagnostic.message));

    return () => {
      // ホットリロード/再マウント時のメモリリークを防ぐ。
      view.destroy();
      viewRef.current = null;
    };
  }, [extensions]);

  /**
   * 親 state（value）が外から更新された場合の片方向同期。
   * 既存 doc と同じ値なら dispatch しない。
   */
  useEffect(() => {
    const view = viewRef.current;
    if (!view) {
      return;
    }
    const current = view.state.doc.toString();
    if (current === value) {
      return;
    }
    view.dispatch({
      changes: {
        from: 0,
        to: current.length,
        insert: value
      }
    });
  }, [value]);

  return (
    <div
      ref={hostRef}
      className="mt-1 min-h-[26rem] overflow-hidden rounded border border-zinc-300 bg-white text-xs"
      aria-label="YAML editor"
    />
  );
}
