/**
 * Builtin action の actionCatalog i18n 正本（canonical actionId 根）。
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
  [`${builtinPrefix}noop`]: { ui: { fields: {} } },
  [`${builtinPrefix}sleep`]: {
    ui: {
      fields: {
        duration: { label: "待機時間" },
      },
    },
  },
  [`${builtinPrefix}rest`]: {
    ui: {
      fields: {
        url: { label: "URL" },
        method: { label: "HTTP メソッド" },
        headers: { label: "ヘッダー" },
        body: { label: "リクエスト本文" },
        timeout: { label: "タイムアウト（秒）" },
        idempotencyKey: { label: "冪等キー" },
      },
    },
  },
  [`${builtinPrefix}notify`]: {
    ui: {
      fields: {
        channel: { label: "チャネル" },
        to: { label: "宛先" },
        subject: { label: "件名" },
        body: { label: "本文" },
        from: { label: "送信元" },
      },
    },
  },
  [`${builtinPrefix}signal`]: {
    ui: {
      fields: {
        target: { label: "ターゲット" },
        signal: { label: "シグナル名" },
      },
    },
  },
  [`${builtinPrefix}publish`]: {
    ui: {
      fields: {
        topic: { label: "トピック" },
        payload: { label: "ペイロード" },
      },
    },
  },
  [`${builtinPrefix}workflow`]: {
    ui: {
      fields: {
        definitionId: { label: "定義 ID" },
        mode: { label: "起動モード" },
        input: { label: "子ワークフロー入力" },
        timeout: { label: "タイムアウト（秒）" },
      },
    },
  },
};

/** Builtin action の英語 actionCatalog 上書き。 */
export const actionCatalogUiTextEnOverrides: ActionCatalogUiText = {
  [`${builtinPrefix}sleep`]: {
    ui: {
      fields: {
        duration: { label: "Duration" },
      },
    },
  },
  [`${builtinPrefix}rest`]: {
    ui: {
      fields: {
        url: { label: "URL" },
        method: { label: "HTTP method" },
        headers: { label: "Headers" },
        body: { label: "Request body" },
        timeout: { label: "Timeout (seconds)" },
        idempotencyKey: { label: "Idempotency key" },
      },
    },
  },
  [`${builtinPrefix}notify`]: {
    ui: {
      fields: {
        channel: { label: "Channel" },
        to: { label: "To" },
        subject: { label: "Subject" },
        body: { label: "Body" },
        from: { label: "From" },
      },
    },
  },
  [`${builtinPrefix}signal`]: {
    ui: {
      fields: {
        target: { label: "Target" },
        signal: { label: "Signal name" },
      },
    },
  },
  [`${builtinPrefix}publish`]: {
    ui: {
      fields: {
        topic: { label: "Topic" },
        payload: { label: "Payload" },
      },
    },
  },
  [`${builtinPrefix}workflow`]: {
    ui: {
      fields: {
        definitionId: { label: "Definition ID" },
        mode: { label: "Start mode" },
        input: { label: "Child workflow input" },
        timeout: { label: "Timeout (seconds)" },
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
