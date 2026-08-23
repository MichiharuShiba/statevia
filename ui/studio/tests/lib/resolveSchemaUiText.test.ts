import { describe, expect, it } from "vitest";
import { actionCatalogUiTextJa, actionCatalogUiTextEnOverrides } from "@/shared/i18n/actionCatalogUiText";
import {
  listRootInputFieldNames,
  resolveLabelKeyFromActionCatalog,
  resolveSchemaUiText
} from "@/features/definition-editor/actionSchema/resolveSchemaUiText";
import { getUiText } from "@/shared/i18n/uiTextLocale";

const sleepActionId = "statevia.action.builtin.execution.sleep";
const sleepDurationLabelKey = `${sleepActionId}.ui.fields.duration.label`;

describe("resolveSchemaUiText", () => {
  it("ja ロケールで uiText.ts の labelKey を解決する", () => {
    const uiText = getUiText("ja");
    expect(resolveSchemaUiText(uiText, sleepDurationLabelKey)).toBe("待機時間");
  });

  it("en ロケールで uiText.en.ts の上書きを解決する", () => {
    const uiText = getUiText("en");
    expect(resolveSchemaUiText(uiText, sleepDurationLabelKey)).toBe("Duration");
  });

  it("en のみ定義の labelKey を ja では Schema フォールバックする", () => {
    const enOnlyKey = "statevia.action.builtin.test.ui.fields.onlyEn.label";
    const uiTextJa = getUiText("ja");
    expect(resolveSchemaUiText(uiTextJa, enOnlyKey, { propertyName: "onlyEn" })).toBe("onlyEn");
  });

  it("未定義 labelKey は fallbackLabel → title → プロパティ名の順で解決する", () => {
    const uiText = getUiText("ja");
    expect(
      resolveSchemaUiText(uiText, "missing.key", {
        fallbackLabel: "Fallback",
        schemaTitle: "Title",
        propertyName: "channel"
      })
    ).toBe("Fallback");
    expect(resolveSchemaUiText(uiText, undefined, { schemaTitle: "Title", propertyName: "channel" })).toBe(
      "Title"
    );
    expect(resolveSchemaUiText(uiText, undefined, { propertyName: "channel" })).toBe("channel");
  });
});

describe("resolveLabelKeyFromActionCatalog", () => {
  it("actionCatalog 辞書から label / description / placeholder を解決する", () => {
    expect(resolveLabelKeyFromActionCatalog(actionCatalogUiTextJa, sleepDurationLabelKey)).toBe(
      "待機時間"
    );
    expect(
      resolveLabelKeyFromActionCatalog(actionCatalogUiTextEnOverrides, sleepDurationLabelKey)
    ).toBe("Duration");
  });

  it("論理パス付き labelKey（ship.address）を解決する", () => {
    const nestedKey = "statevia.action.builtin.test.ui.fields.ship.address.label";
    const catalog = {
      "statevia.action.builtin.test": {
        ui: {
          fields: {
            "ship.address": { label: "配送先住所" }
          }
        }
      }
    };
    expect(resolveLabelKeyFromActionCatalog(catalog, nestedKey)).toBe("配送先住所");
  });
});

describe("listRootInputFieldNames", () => {
  it("fieldOrder を優先してルート property を列挙する", () => {
    const names = listRootInputFieldNames(
      {
        type: "object",
        properties: {
          url: { type: "string" },
          method: { type: "string" },
          body: { type: "string" }
        }
      },
      ["method", "url"]
    );
    expect(names).toEqual(["method", "url", "body"]);
  });
});
