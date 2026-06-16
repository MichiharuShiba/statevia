"use client";

import { useEffect, useMemo } from "react";
import { listRootInputFieldNames, resolveSchemaUiText } from "../../lib/actionSchema/resolveSchemaUiText";
import { coerceScalarForSchema, normalizeActionInputRecord } from "../../lib/actionSchema/coerceActionInputValue";
import type {
  ActionFieldUiHints,
  ActionInputValidationDetail,
  ActionSchemaDetailResponse,
  JsonSchemaObject
} from "../../lib/actionSchema/types";
import { useUiText } from "../../lib/uiTextContext";

/** Schema 駆動 action input フォームの props。 */
export type SchemaDrivenActionInputFormProps = {
  /** canonical actionId。 */
  actionId: string;
  /** Schema API 詳細応答。 */
  schemaDetail: ActionSchemaDetailResponse;
  /** 現在の input マップ（path 式は文字列として保持）。 */
  value: Record<string, unknown>;
  /** 値変更時コールバック。 */
  onChange: (nextValue: Record<string, unknown>) => void;
  /** 422 詳細（選択ノード向け）。 */
  validationDetails?: ActionInputValidationDetail[];
};

/**
 * inputSchema + uiMetadata から MVP widget セットのフォームを生成する。
 */
export function SchemaDrivenActionInputForm({
  actionId,
  schemaDetail,
  value,
  onChange,
  validationDetails = []
}: Readonly<SchemaDrivenActionInputFormProps>) {
  const uiText = useUiText();
  const inputSchema = schemaDetail.schema.inputSchema;
  const fieldNames = useMemo(
    () => listRootInputFieldNames(inputSchema, schemaDetail.uiMetadata?.fieldOrder),
    [inputSchema, schemaDetail.uiMetadata?.fieldOrder]
  );

  const normalizedValue = useMemo(
    () => normalizeActionInputRecord(value, inputSchema, fieldNames),
    [fieldNames, inputSchema, value]
  );

  useEffect(() => {
    const changed = fieldNames.some((fieldName) => normalizedValue[fieldName] !== value[fieldName]);
    if (!changed) {
      return;
    }
    onChange(normalizedValue);
  }, [fieldNames, normalizedValue, onChange, value]);

  const errorsByProperty = useMemo(() => {
    const map = new Map<string, string>();
    for (const detail of validationDetails) {
      const propertyName = jsonPathToPropertyName(detail.jsonPath);
      if (propertyName && detail.message) {
        map.set(propertyName, detail.message);
      }
    }
    return map;
  }, [validationDetails]);

  if (fieldNames.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      {fieldNames.map((propertyName) => {
        const propertySchema = inputSchema.properties?.[propertyName];
        if (!propertySchema) {
          return null;
        }
        const hints = schemaDetail.uiMetadata?.fields?.[propertyName];
        const label = resolveSchemaUiText(uiText, hints?.labelKey, {
          schemaTitle: propertySchema.title,
          propertyName
        });
        const description = hints?.descriptionKey
          ? resolveSchemaUiText(uiText, hints.descriptionKey)
          : propertySchema.description ?? "";
        const placeholder = hints?.placeholderKey
          ? resolveSchemaUiText(uiText, hints.placeholderKey)
          : undefined;
        const fieldValue = normalizedValue[propertyName];
        const error = errorsByProperty.get(propertyName);

        return (
          <label key={propertyName} className="block text-xs">
            <span className="block font-medium">{label}</span>
            {description ? (
              <span className="mt-0.5 block text-[10px] text-[var(--md-sys-color-on-surface-variant)]">
                {description}
              </span>
            ) : null}
            {renderFieldControl({
              actionId,
              propertyName,
              propertySchema,
              hints,
              fieldValue,
              placeholder,
              onFieldChange: (nextFieldValue) => {
                const next = { ...normalizedValue };
                if (nextFieldValue === undefined || nextFieldValue === "") {
                  delete next[propertyName];
                } else {
                  next[propertyName] = nextFieldValue;
                }
                onChange(next);
              }
            })}
            {error ? <p className="mt-0.5 text-[10px] text-rose-600">{error}</p> : null}
          </label>
        );
      })}
    </div>
  );
}

type RenderFieldControlArgs = {
  actionId: string;
  propertyName: string;
  propertySchema: JsonSchemaObject;
  hints?: ActionFieldUiHints | null;
  fieldValue: unknown;
  placeholder?: string;
  onFieldChange: (nextValue: unknown) => void;
};

function renderFieldControl({
  propertySchema,
  hints,
  fieldValue,
  placeholder,
  onFieldChange
}: RenderFieldControlArgs) {
  const widget = hints?.widget ?? inferWidget(propertySchema);
  const stringValue = formatScalarFieldDisplayValue(fieldValue);
  const inputClassName =
    "mt-1 w-full rounded border border-[var(--md-sys-color-outline)] px-2 py-1 text-xs";

  if (widget === "select" && propertySchema.enum && propertySchema.enum.length > 0) {
    return (
      <select
        className={inputClassName}
        value={stringValue}
        onChange={(event) => onFieldChange(event.target.value || undefined)}
      >
        <option value="">—</option>
        {propertySchema.enum.map((entry) => {
          const optionValue = formatScalarFieldDisplayValue(entry);
          return (
            <option key={optionValue} value={optionValue}>
              {optionValue}
            </option>
          );
        })}
      </select>
    );
  }

  if (propertySchema.type === "object") {
    const objectText =
      fieldValue !== undefined && fieldValue !== null
        ? JSON.stringify(fieldValue, null, 2)
        : "";
    return (
      <textarea
        className={`${inputClassName} min-h-[4rem] font-mono`}
        value={objectText}
        placeholder={placeholder ?? "{}"}
        onChange={(event) => {
          const text = event.target.value.trim();
          if (!text) {
            onFieldChange(undefined);
            return;
          }
          try {
            onFieldChange(JSON.parse(text) as unknown);
          } catch {
            onFieldChange(event.target.value);
          }
        }}
      />
    );
  }

  const inputType = resolveInputType(widget, hints, propertySchema);

  return (
    <input
      className={inputClassName}
      type={inputType}
      value={stringValue}
      placeholder={placeholder ?? valueKindPlaceholder(propertySchema)}
      onChange={(event) => {
        const raw = event.target.value;
        if (!raw.trim()) {
          onFieldChange(undefined);
          return;
        }
        onFieldChange(coerceScalarForSchema(raw, propertySchema));
      }}
    />
  );
}

function formatScalarFieldDisplayValue(fieldValue: unknown): string {
  if (fieldValue === undefined || fieldValue === null) {
    return "";
  }
  if (
    typeof fieldValue === "string" ||
    typeof fieldValue === "number" ||
    typeof fieldValue === "boolean"
  ) {
    return String(fieldValue);
  }
  return JSON.stringify(fieldValue);
}

function resolveInputType(
  widget: string,
  hints: ActionFieldUiHints | null | undefined,
  propertySchema: JsonSchemaObject
): string {
  if (widget === "secret" || hints?.sensitive) {
    return "password";
  }
  if (widget === "url") {
    return "url";
  }
  if (propertySchema.type === "integer" || propertySchema.type === "number") {
    return "number";
  }
  return "text";
}

function inferWidget(propertySchema: JsonSchemaObject): string {
  if (propertySchema.enum && propertySchema.enum.length > 0) {
    return "select";
  }
  if (propertySchema.format === "uri" || propertySchema.format === "url") {
    return "url";
  }
  if (propertySchema.type === "object") {
    return "text";
  }
  return "text";
}

function valueKindPlaceholder(propertySchema: JsonSchemaObject): string | undefined {
  const valueKind = propertySchema["x-statevia-valueKind"];
  if (valueKind === "path") {
    return "$.path.to.value";
  }
  if (valueKind === "literalOrPath") {
    return "literal or $.path";
  }
  return undefined;
}

function jsonPathToPropertyName(jsonPath: string | undefined): string | undefined {
  if (!jsonPath) {
    return undefined;
  }
  const prefix = "$.input.";
  if (!jsonPath.startsWith(prefix)) {
    return undefined;
  }
  const remainder = jsonPath.slice(prefix.length);
  const dotIndex = remainder.indexOf(".");
  return dotIndex >= 0 ? remainder.slice(0, dotIndex) : remainder;
}
