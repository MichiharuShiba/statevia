/**
 * Builtin action の actionCatalog i18n 正本（canonical actionId 根）。
 * rest / notify の固定辞書は持たない（Module Publication の UiMetadata を正とする）。
 * labelKey 形式: `{actionId}.ui.fields.{field}.label`
 */
export type ActionCatalogFieldUiText = {
  label: string;
  description?: string;
  placeholder?: string;
};

/** canonical actionId 根の actionCatalog 辞書（`uiText.actionCatalog` の型）。 */
export type ActionCatalogUiText = Record<
  string,
  {
    ui: {
      fields: Record<string, ActionCatalogFieldUiText>;
    };
  }
>;

const builtinPrefix = "statevia.action.builtin.";

/** Builtin action の日本語 actionCatalog 文言。 */
export const actionCatalogUiTextJa: ActionCatalogUiText = {
  [`${builtinPrefix}execution.noop`]: { ui: { fields: {} } },
  [`${builtinPrefix}execution.sleep`]: {
    ui: {
      fields: {
        duration: { label: "待機時間" },
      },
    },
  },
  [`${builtinPrefix}execution.signal`]: {
    ui: {
      fields: {
        target: { label: "ターゲット" },
        signal: { label: "シグナル名" },
      },
    },
  },
  [`${builtinPrefix}event.publish`]: {
    ui: {
      fields: {
        topic: { label: "トピック" },
        payload: { label: "ペイロード" },
      },
    },
  },
  [`${builtinPrefix}workflow.invoke`]: {
    ui: {
      fields: {
        definitionId: { label: "定義 ID" },
        input: { label: "子ワークフロー入力" },
      },
    },
  },
};

/** Builtin action の英語 actionCatalog 上書き。 */
export const actionCatalogUiTextEnOverrides: ActionCatalogUiText = {
  [`${builtinPrefix}execution.sleep`]: {
    ui: {
      fields: {
        duration: { label: "Duration" },
      },
    },
  },
  [`${builtinPrefix}execution.signal`]: {
    ui: {
      fields: {
        target: { label: "Target" },
        signal: { label: "Signal name" },
      },
    },
  },
  [`${builtinPrefix}event.publish`]: {
    ui: {
      fields: {
        topic: { label: "Topic" },
        payload: { label: "Payload" },
      },
    },
  },
  [`${builtinPrefix}workflow.invoke`]: {
    ui: {
      fields: {
        definitionId: { label: "Definition ID" },
        input: { label: "Child workflow input" },
      },
    },
  },
};

/**
 * labelKey（`{actionId}.ui.fields.{field}.label`）を actionCatalog から解決する。
 * Phase F の resolveSchemaUiText 実装前の簡易ヘルパー。
 */
export function resolveActionCatalogLabel(
  actionCatalog: ActionCatalogUiText,
  labelKey: string,
): string | undefined {
  const suffix = ".ui.fields.";
  const suffixIndex = labelKey.indexOf(suffix);
  if (suffixIndex <= 0) {
    return undefined;
  }

  const actionId = labelKey.slice(0, suffixIndex);
  const remainder = labelKey.slice(suffixIndex + suffix.length);
  const fieldName = remainder.split(".")[0];
  return actionCatalog[actionId]?.ui.fields[fieldName]?.label;
}
