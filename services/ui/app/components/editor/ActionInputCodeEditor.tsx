"use client";

import { json } from "@codemirror/lang-json";
import { defaultKeymap, history, historyKeymap, indentWithTab } from "@codemirror/commands";
import { Compartment, EditorState, type Extension } from "@codemirror/state";
import { EditorView, keymap, placeholder as cmPlaceholder } from "@codemirror/view";
import type { MutableRefObject } from "react";
import { useEffect, useRef } from "react";

const FONT_STACK =
  "ui-monospace, SFMono-Regular, SF Mono, Menlo, Consolas, Liberation Mono, monospace";

/** ActionInputSyntaxHighlight の型定義。 */
export type ActionInputSyntaxHighlight = "auto" | "jsonOnly";

/** アクション input 用 CodeMirror エディタの props。 */
export type ActionInputCodeEditorProps = {
  /** 編集テキスト（パス文字列または JSON オブジェクト文字列） */
  value: string;
  onChange: (nextValue: string) => void;
  /** フォーカス喪失時のエディタ全文（親の state とずれることがないよう渡す） */
  onBlur?: (latestText: string) => void;
  placeholder?: string;
  /**
   * `auto`: `{` / `[` で始まるときだけ JSON ハイライト（action のパス文字列と併用）。
   * `jsonOnly`: 常に JSON ハイライト（実行開始時の input など）。
   */
  syntaxHighlight?: ActionInputSyntaxHighlight;
  /** アクセシビリティ用ラベル */
  ariaLabel?: string;
  /** ラッパーに追加するクラス（高さなど） */
  className?: string;
};

/**
 * `{` / `[` で始まる内容には JSON ハイライトを付与し、それ以外はプレーンテキストとして表示する。
 */
function looksLikeJsonObjectOrArray(text: string): boolean {
  const trimmed = text.trimStart();
  return trimmed.startsWith("{") || trimmed.startsWith("[");
}

function createExtensionsJsonOnly(
  languageCompartment: Compartment,
  lastJsonMode: MutableRefObject<boolean | null>,
  onChangeRef: MutableRefObject<(next: string) => void>,
  onBlurRef: MutableRefObject<((latestText: string) => void) | undefined>,
  placeholderText: string | undefined,
  _initialDoc: string
): Extension[] {
  lastJsonMode.current = true;

  return [
    history(),
    keymap.of([indentWithTab, ...defaultKeymap, ...historyKeymap]),
    languageCompartment.of(json()),
    EditorView.theme({
      "&": {
        fontFamily: FONT_STACK,
        fontSize: "11px"
      },
      ".cm-content": {
        padding: "6px 8px",
        minHeight: "5rem",
        caretColor: "var(--editor-caret)"
      },
      ".cm-line": {
        padding: "0 2px"
      },
      ".cm-focused .cm-cursor": {
        borderLeftColor: "var(--editor-caret-border)"
      },
      ".cm-placeholder": {
        color: "var(--md-sys-color-on-surface-variant)",
        opacity: "0.75"
      }
    }),
    EditorView.lineWrapping,
    ...(placeholderText ? [cmPlaceholder(placeholderText)] : []),
    EditorView.domEventHandlers({
      blur: (_event, view) => {
        onBlurRef.current?.(view.state.doc.toString());
      }
    }),
    EditorView.updateListener.of((update) => {
      if (!update.docChanged) {
        return;
      }
      onChangeRef.current(update.state.doc.toString());
    })
  ];
}

function createExtensionsAuto(
  languageCompartment: Compartment,
  lastJsonMode: MutableRefObject<boolean | null>,
  onChangeRef: MutableRefObject<(next: string) => void>,
  onBlurRef: MutableRefObject<((latestText: string) => void) | undefined>,
  placeholderText: string | undefined,
  initialDoc: string
): Extension[] {
  const initialJson = looksLikeJsonObjectOrArray(initialDoc);
  lastJsonMode.current = initialJson;

  return [
    history(),
    keymap.of([indentWithTab, ...defaultKeymap, ...historyKeymap]),
    languageCompartment.of(initialJson ? json() : []),
    EditorView.theme({
      "&": {
        fontFamily: FONT_STACK,
        fontSize: "11px"
      },
      ".cm-content": {
        padding: "6px 8px",
        minHeight: "5rem",
        caretColor: "var(--editor-caret)"
      },
      ".cm-line": {
        padding: "0 2px"
      },
      ".cm-focused .cm-cursor": {
        borderLeftColor: "var(--editor-caret-border)"
      },
      ".cm-placeholder": {
        color: "var(--md-sys-color-on-surface-variant)",
        opacity: "0.75"
      }
    }),
    EditorView.lineWrapping,
    ...(placeholderText ? [cmPlaceholder(placeholderText)] : []),
    EditorView.domEventHandlers({
      blur: (_event, view) => {
        onBlurRef.current?.(view.state.doc.toString());
      }
    }),
    EditorView.updateListener.of((update) => {
      if (!update.docChanged) {
        return;
      }
      const text = update.state.doc.toString();
      onChangeRef.current(text);

      const wantJson = looksLikeJsonObjectOrArray(text);
      if (lastJsonMode.current === wantJson) {
        return;
      }
      lastJsonMode.current = wantJson;
      queueMicrotask(() => {
        update.view.dispatch({
          effects: languageCompartment.reconfigure(wantJson ? json() : [])
        });
      });
    })
  ];
}

/**
 * action ノードの input 用ミニ CodeMirror（`syntaxHighlight` 既定は JSON / パス自動判定）。
 * `jsonOnly` では常に JSON シンタックスを付与する。
 */
export function ActionInputCodeEditor({
  value,
  onChange,
  onBlur,
  placeholder,
  syntaxHighlight = "auto",
  ariaLabel = "Action input editor",
  className
}: Readonly<ActionInputCodeEditorProps>) {
  const hostRef = useRef<HTMLDivElement | null>(null);
  const viewRef = useRef<EditorView | null>(null);
  const languageCompartment = useRef(new Compartment());
  const lastJsonMode = useRef<boolean | null>(null);
  const onChangeRef = useRef(onChange);
  const onBlurRef = useRef(onBlur);
  onChangeRef.current = onChange;
  onBlurRef.current = onBlur;

  useEffect(() => {
    const host = hostRef.current;
    if (!host) {
      return;
    }

    const initialDoc = value;
    let extensions: Extension[];
    if (syntaxHighlight === "jsonOnly") {
      extensions = createExtensionsJsonOnly(
        languageCompartment.current,
        lastJsonMode,
        onChangeRef,
        onBlurRef,
        placeholder,
        initialDoc
      );
    } else if (syntaxHighlight === "auto") {
      extensions = createExtensionsAuto(
        languageCompartment.current,
        lastJsonMode,
        onChangeRef,
        onBlurRef,
        placeholder,
        initialDoc
      );
    } else {
      throw new Error(`Unsupported syntaxHighlight: ${String(syntaxHighlight)}`);
    }

    const state = EditorState.create({
      doc: initialDoc,
      extensions
    });

    const view = new EditorView({
      state,
      parent: host
    });
    viewRef.current = view;

    return () => {
      view.destroy();
      viewRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- 初回のみ生成。ノード切替は親の key で別インスタンスにする。
  }, []);

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
      changes: { from: 0, to: current.length, insert: value }
    });

    if (syntaxHighlight === "jsonOnly") {
      if (lastJsonMode.current !== true) {
        lastJsonMode.current = true;
        view.dispatch({
          effects: languageCompartment.current.reconfigure(json())
        });
      }
      return;
    } else if (syntaxHighlight !== "auto") {
      throw new Error(`Unsupported syntaxHighlight: ${String(syntaxHighlight)}`);
    }

    const wantJson = looksLikeJsonObjectOrArray(value);
    if (lastJsonMode.current !== wantJson) {
      lastJsonMode.current = wantJson;
      view.dispatch({
        effects: languageCompartment.current.reconfigure(wantJson ? json() : [])
      });
    }
  }, [value, syntaxHighlight]);

  const wrapperClassName = [
    "mt-1 overflow-hidden rounded border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] text-xs",
    className ?? ""
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div ref={hostRef} className={wrapperClassName} aria-label={ariaLabel} />
  );
}
