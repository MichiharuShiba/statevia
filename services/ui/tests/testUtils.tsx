import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement } from "react";
import { UiTextProvider } from "../app/lib/uiTextContext";
import type { Locale } from "../app/lib/i18n";

type RenderWithUiTextOptions = RenderOptions & {
  locale?: Locale;
};

/**
 * `UiTextProvider` でラップしてコンポーネントを描画する。
 */
export function renderWithUiText(ui: ReactElement, options: RenderWithUiTextOptions = {}) {
  const { locale = "ja", ...renderOptions } = options;
  return render(<UiTextProvider locale={locale}>{ui}</UiTextProvider>, renderOptions);
}
