import type { ActionCatalogUiText } from "../actionCatalogUiText";
import type { UiText } from "../uiText";
import type { JsonSchemaObject } from "./types";

export type ResolveSchemaUiTextOptions = {
  /** Schema 任意のフォールバック表示名。 */
  fallbackLabel?: string;
  /** JSON Schema の title。 */
  schemaTitle?: string;
  /** プロパティ名。 */
  propertyName?: string;
};

/**
 * labelKey を現在ロケールの uiText のみから解決する（他ロケールへ自動フォールバックしない）。
 * 未解決時は fallbackLabel → title → プロパティ名の順で返す。
 */
export function resolveSchemaUiText(
  uiText: UiText,
  labelKey: string | undefined | null,
  options: ResolveSchemaUiTextOptions = {}
): string {
  if (labelKey) {
    const fromCatalog = resolveLabelKeyFromActionCatalog(uiText.actionCatalog, labelKey);
    if (fromCatalog) {
      return fromCatalog;
    }
  }

  if (options.fallbackLabel && options.fallbackLabel.trim().length > 0) {
    return options.fallbackLabel;
  }
  if (options.schemaTitle && options.schemaTitle.trim().length > 0) {
    return options.schemaTitle;
  }
  if (options.propertyName && options.propertyName.trim().length > 0) {
    return options.propertyName;
  }
  return "";
}

/**
 * actionCatalog 辞書から labelKey を解決する。
 */
export function resolveLabelKeyFromActionCatalog(
  actionCatalog: ActionCatalogUiText,
  labelKey: string
): string | undefined {
  const match = /^(.+)\.ui\.fields\.([^.]+)\.(label|description|placeholder)$/.exec(labelKey);
  if (!match) {
    return undefined;
  }
  const [, actionId, fieldName, kind] = match;
  const entry = actionCatalog[actionId];
  const field = entry?.ui?.fields?.[fieldName];
  if (!field) {
    return undefined;
  }
  switch (kind) {
    case "label":
      return field.label;
    case "description":
      return field.description;
    case "placeholder":
      return field.placeholder;
    default:
      return undefined;
  }
}

/**
 * inputSchema からルート直下のフィールド名を fieldOrder 優先で列挙する。
 */
export function listRootInputFieldNames(
  inputSchema: JsonSchemaObject,
  fieldOrder?: string[] | null
): string[] {
  const properties = inputSchema.properties ?? {};
  const propertyNames = Object.keys(properties);
  if (!fieldOrder || fieldOrder.length === 0) {
    return propertyNames;
  }
  const ordered = fieldOrder.filter((name) => propertyNames.includes(name));
  const remainder = propertyNames.filter((name) => !ordered.includes(name));
  return [...ordered, ...remainder];
}
