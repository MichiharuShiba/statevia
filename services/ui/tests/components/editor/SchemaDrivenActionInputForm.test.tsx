import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SchemaDrivenActionInputForm } from "../../../app/components/editor/SchemaDrivenActionInputForm";
import type { ActionSchemaDetailResponse } from "../../../app/lib/actionSchema/types";
import { UiTextProvider } from "../../../app/lib/uiTextContext";

const restSchemaDetail: ActionSchemaDetailResponse = {
  descriptor: {
    actionId: "statevia.action.builtin.rest",
    version: "1.0.0",
    displayName: "REST"
  },
  schema: {
    schemaVersion: "2020-12",
    inputSchema: {
      type: "object",
      properties: {
        url: { type: "string", title: "url" },
        method: {
          type: "string",
          enum: ["GET", "POST"]
        }
      },
      required: ["url", "method"]
    },
    outputSchema: { type: "object" }
  },
  uiMetadata: {
    fieldOrder: ["url", "method"],
    fields: {
      url: {
        widget: "url",
        labelKey: "statevia.action.builtin.rest.ui.fields.url.label"
      },
      method: {
        widget: "select",
        labelKey: "statevia.action.builtin.rest.ui.fields.method.label"
      }
    }
  }
};

function renderForm(
  value: Record<string, unknown> = {},
  onChange: (next: Record<string, unknown>) => void = vi.fn()
) {
  return render(
    <UiTextProvider locale="ja">
      <SchemaDrivenActionInputForm
        actionId="statevia.action.builtin.rest"
        schemaDetail={restSchemaDetail}
        value={value}
        onChange={onChange}
      />
    </UiTextProvider>
  );
}

describe("SchemaDrivenActionInputForm", () => {
  it("rest action の url / method フィールドをフォーム表示する", () => {
    renderForm();
    expect(screen.getByText("URL")).toBeInTheDocument();
    expect(screen.getByText("HTTP メソッド")).toBeInTheDocument();
  });

  it("入力変更を onChange で親へ通知する", () => {
    const onChange = vi.fn();
    const { container } = renderForm({}, onChange);
    const urlInput = container.querySelector('input[type="url"]');
    expect(urlInput).toBeTruthy();
    fireEvent.change(urlInput!, { target: { value: "https://example.com" } });
    expect(onChange).toHaveBeenCalledWith({ url: "https://example.com" });
  });

  it("timeout 入力は integer として親へ通知する", () => {
    const onChange = vi.fn();
    const schemaWithTimeout = {
      ...restSchemaDetail,
      schema: {
        ...restSchemaDetail.schema,
        inputSchema: {
          ...restSchemaDetail.schema.inputSchema,
          properties: {
            ...restSchemaDetail.schema.inputSchema.properties,
            timeout: { type: "integer", "x-statevia-valueKind": "literalOrPath" }
          }
        }
      },
      uiMetadata: {
        ...restSchemaDetail.uiMetadata,
        fieldOrder: ["url", "method", "timeout"],
        fields: {
          ...restSchemaDetail.uiMetadata?.fields,
          timeout: {
            widget: "text",
            labelKey: "statevia.action.builtin.rest.ui.fields.timeout.label"
          }
        }
      }
    };
    render(
      <UiTextProvider locale="ja">
        <SchemaDrivenActionInputForm
          actionId="statevia.action.builtin.rest"
          schemaDetail={schemaWithTimeout}
          value={{}}
          onChange={onChange}
        />
      </UiTextProvider>
    );
    const timeoutLabel = screen.getByText("タイムアウト（秒）");
    const timeoutInput = timeoutLabel.parentElement?.querySelector("input");
    expect(timeoutInput).toBeTruthy();
    fireEvent.change(timeoutInput!, { target: { value: "30" } });
    expect(onChange).toHaveBeenCalledWith({ timeout: 30 });
  });
});
