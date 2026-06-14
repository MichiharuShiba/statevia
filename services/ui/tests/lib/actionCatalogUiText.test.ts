import { describe, expect, it } from "vitest";
import {
  actionCatalogUiTextEnOverrides,
  actionCatalogUiTextJa,
  resolveActionCatalogLabel,
} from "../../app/lib/actionCatalogUiText";
import { uiTextEn } from "../../app/lib/uiText.en";
import { uiTextJa } from "../../app/lib/uiText";

describe("actionCatalogUiText", () => {
  it("主要 builtin action の labelKey を ja ロケールで解決する", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextJa.actionCatalog,
        "statevia.action.builtin.rest.ui.fields.url.label",
      ),
    ).toBe("URL");
    expect(
      resolveActionCatalogLabel(
        uiTextJa.actionCatalog,
        "statevia.action.builtin.notify.ui.fields.subject.label",
      ),
    ).toBe("件名");
  });

  it("主要 builtin action の labelKey を en ロケールで解決する", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextEn.actionCatalog,
        "statevia.action.builtin.rest.ui.fields.method.label",
      ),
    ).toBe("HTTP method");
    expect(
      resolveActionCatalogLabel(
        uiTextEn.actionCatalog,
        "statevia.action.builtin.workflow.ui.fields.mode.label",
      ),
    ).toBe("Start mode");
  });

  it("英語上書きがない action は ja 文言を継承する", () => {
    expect(
      resolveActionCatalogLabel(
        uiTextEn.actionCatalog,
        "statevia.action.builtin.noop.ui.fields.missing.label",
      ),
    ).toBeUndefined();
    expect(actionCatalogUiTextEnOverrides["statevia.action.builtin.noop"]).toBeUndefined();
    expect(actionCatalogUiTextJa["statevia.action.builtin.rest"]).toBeDefined();
  });
});
