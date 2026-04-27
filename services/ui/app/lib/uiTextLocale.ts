import type { Locale } from "./i18n";
import { uiTextEn } from "./uiText.en";
import { uiTextJa, type UiText } from "./uiText";

/**
 * 現在ロケールに対応する UI 文言辞書を返す。
 */
export function getUiText(locale: Locale): UiText {
  return locale === "en" ? uiTextEn : uiTextJa;
}

