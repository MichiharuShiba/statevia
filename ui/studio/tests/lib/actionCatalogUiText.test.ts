import { describe, expect, it } from "vitest";
import {
  actionCatalogUiTextEnOverrides,
  actionCatalogUiTextJa,
  resolveActionCatalogLabel,
} from "@/shared/i18n/actionCatalogUiText";
import { uiTextEn } from "@/shared/i18n/uiText.en";
import { uiTextJa } from "@/shared/i18n/uiText";

describe("actionCatalogUiText", () => {
  it("主要 builtin action の labelKey を ja ロケールで解決する", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextJa.actionCatalog,
        "statevia.action.builtin.execution.sleep.ui.fields.duration.label",
      ),
    ).toBe("待機時間");
    expect(
      resolveActionCatalogLabel(
        uiTextJa.actionCatalog,
        "statevia.action.builtin.workflow.invoke.ui.fields.definitionId.label",
      ),
    ).toBe("定義 ID");
  });

  it("主要 builtin action の labelKey を en ロケールで解決する", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextEn.actionCatalog,
        "statevia.action.builtin.execution.sleep.ui.fields.duration.label",
      ),
    ).toBe("Duration");
    expect(
      resolveActionCatalogLabel(
        uiTextEn.actionCatalog,
        "statevia.action.builtin.workflow.invoke.ui.fields.definitionId.label",
      ),
    ).toBe("Definition ID");
  });

  it("英語上書きがない action は ja 文言を継承する", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextEn.actionCatalog,
        "statevia.action.builtin.execution.noop.ui.fields.missing.label",
      ),
    ).toBeUndefined();
    expect(actionCatalogUiTextEnOverrides["statevia.action.builtin.execution.noop"]).toBeUndefined();
    expect(actionCatalogUiTextJa["statevia.action.builtin.execution.sleep"]).toBeDefined();
  });

  it("labelKey に .ui.fields. が無い場合は undefined を返す", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextJa.actionCatalog,
        "statevia.action.builtin.execution.sleep.ui.labels.duration",
      ),
    ).toBeUndefined();
  });

  it("存在しない field の labelKey は undefined を返す", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextJa.actionCatalog,
        "statevia.action.builtin.execution.sleep.ui.fields.unknown.label",
      ),
    ).toBeUndefined();
  });
});
