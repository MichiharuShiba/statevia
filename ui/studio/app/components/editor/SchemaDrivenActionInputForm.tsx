"use client";

import { useEffect, useMemo, useRef } from "react";
import { coerceScalarForSchema } from "../../lib/actionSchema/coerceActionInputValue";
import {
  buildInputFieldTree,
  getNestedInputValue,
  jsonPathToLogicalPath,
  setNestedInputValue,
  type InputFieldNode
} from "../../lib/actionSchema/nestedInputPaths";
import { resolveSchemaUiText } from "../../lib/actionSchema/resolveSchemaUiText";
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
  /** 現在の input マップ（ネスト map 形式。path 式は文字列として保持）。 */
  value: Record<string, unknown>;
  /** 値変更時コールバック。 */
  onChange: (nextValue: Record<string, unknown>) => void;
  /** 422 詳細（選択ノード向け）。 */
  validationDetails?: ActionInputValidationDetail[];
};

/**
 * inputSchema + uiMetadata から MVP widget セットのフォームを生成する。
 * `type: object` はネストフィールドグループとして表示する。
 */
export function SchemaDrivenActionInputForm({
  actionId,
  schemaDetail,
  value,
  onChange,
  validationDetails = []
}: Readonly<SchemaDrivenActionInputFormProps>) {
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const inputSchema = schemaDetail.schema.inputSchema;
  const fieldTree = useMemo(
    () => buildInputFieldTree(inputSchema, schemaDetail.uiMetadata?.fieldOrder),
    [inputSchema, schemaDetail.uiMetadata?.fieldOrder]
  );

  const normalizedValue = useMemo(
    () => normalizeNestedActionInputRecord(value, fieldTree),
    [fieldTree, value]
  );

  useEffect(() => {
    const changed = collectScalarPaths(fieldTree).some(
      (logicalPath) => getNestedInputValue(normalizedValue, logicalPath) !== getNestedInputValue(value, logicalPath)
    );
    if (!changed) {
      return;
    }
    onChangeRef.current(normalizedValue);
  }, [fieldTree, normalizedValue, value]);

  const errorsByLogicalPath = useMemo(() => {
    const map = new Map<string, string>();
    for (const detail of validationDetails) {
      const logicalPath = jsonPathToLogicalPath(detail.jsonPath);
      if (logicalPath && detail.message) {
        map.set(logicalPath, detail.message);
      }
    }
    return map;
  }, [validationDetails]);

  if (fieldTree.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      {fieldTree.map((node) => (
        <InputFieldTreeNode
          key={node.logicalPath}
          actionId={actionId}
          node={node}
          uiFields={schemaDetail.uiMetadata?.fields}
          normalizedValue={normalizedValue}
          errorsByLogicalPath={errorsByLogicalPath}
          onChange={onChange}
        />
      ))}
    </div>
  );
}

type InputFieldTreeNodeProps = {
  actionId: string;
  node: InputFieldNode;
  uiFields?: Record<string, ActionFieldUiHints | null> | null;
  normalizedValue: Record<string, unknown>;
  errorsByLogicalPath: Map<string, string>;
  onChange: (nextValue: Record<string, unknown>) => void;
};

function InputFieldTreeNode({
  actionId,
  node,
  uiFields,
  normalizedValue,
  errorsByLogicalPath,
  onChange
}: Readonly<InputFieldTreeNodeProps>) {
  const uiText = useUiText();

  if (node.kind === "group") {
    const hints = uiFields?.[node.logicalPath];
    const label = resolveSchemaUiText(uiText, hints?.labelKey, {
      schemaTitle: node.propertySchema.title,
      propertyName: node.logicalPath.split(".").at(-1)
    });
    return (
      <fieldset className="rounded border border-[var(--md-sys-color-outline-variant)] p-2">
        <legend className="px-1 text-xs font-medium">{label}</legend>
        <div className="space-y-2">
          {node.children.map((child) => (
            <InputFieldTreeNode
              key={child.logicalPath}
              actionId={actionId}
              node={child}
              uiFields={uiFields}
              normalizedValue={normalizedValue}
              errorsByLogicalPath={errorsByLogicalPath}
              onChange={onChange}
            />
          ))}
        </div>
      </fieldset>
    );
  }

  const propertyName = node.logicalPath.split(".").at(-1) ?? node.logicalPath;
  const hints = uiFields?.[node.logicalPath];
  const label = resolveSchemaUiText(uiText, hints?.labelKey, {
    schemaTitle: node.propertySchema.title,
    propertyName
  });
  const description = hints?.descriptionKey
    ? resolveSchemaUiText(uiText, hints.descriptionKey)
    : node.propertySchema.description ?? "";
  const placeholder = hints?.placeholderKey
    ? resolveSchemaUiText(uiText, hints.placeholderKey)
    : valueKindPlaceholder(node.propertySchema, {
        path: uiText.definitionEditor.graph.schemaPathPlaceholder,
        literalOrPath: uiText.definitionEditor.graph.schemaLiteralOrPathPlaceholder
      });
  const fieldValue = getNestedInputValue(normalizedValue, node.logicalPath);
  const error = errorsByLogicalPath.get(node.logicalPath);

  return (
    <label className="block text-xs">
      <span className="block font-medium">{label}</span>
      {description ? (
        <span className="mt-0.5 block text-[10px] text-[var(--md-sys-color-on-surface-variant)]">
          {description}
        </span>
      ) : null}
      {renderFieldControl({
        propertySchema: node.propertySchema,
        hints,
        fieldValue,
        placeholder,
        onFieldChange: (nextFieldValue) => {
          onChange(setNestedInputValue(normalizedValue, node.logicalPath, nextFieldValue));
        }
      })}
      {error ? <p className="mt-0.5 text-[10px] text-rose-600">{error}</p> : null}
    </label>
  );
}

type RenderFieldControlArgs = {
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

  const inputType = resolveInputType(widget, hints, propertySchema);

  return (
    <input
      className={inputClassName}
      type={inputType}
      value={stringValue}
      placeholder={placeholder}
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

function normalizeNestedActionInputRecord(
  value: Record<string, unknown>,
  fieldTree: InputFieldNode[]
): Record<string, unknown> {
  let result = { ...value };
  for (const logicalPath of collectScalarPaths(fieldTree)) {
    const fieldValue = getNestedInputValue(result, logicalPath);
    if (typeof fieldValue !== "string") {
      continue;
    }
    const propertySchema = findScalarSchema(fieldTree, logicalPath);
    if (!propertySchema) {
      continue;
    }
    const coerced = coerceScalarForSchema(fieldValue, propertySchema);
    if (coerced !== fieldValue) {
      result = setNestedInputValue(result, logicalPath, coerced);
    }
  }
  return result;
}

function collectScalarPaths(nodes: InputFieldNode[]): string[] {
  const paths: string[] = [];
  for (const node of nodes) {
    if (node.kind === "scalar") {
      paths.push(node.logicalPath);
      continue;
    }
    paths.push(...collectScalarPaths(node.children));
  }
  return paths;
}

function findScalarSchema(nodes: InputFieldNode[], logicalPath: string): JsonSchemaObject | undefined {
  for (const node of nodes) {
    if (node.kind === "scalar" && node.logicalPath === logicalPath) {
      return node.propertySchema;
    }
    if (node.kind === "group") {
      const nested = findScalarSchema(node.children, logicalPath);
      if (nested) {
        return nested;
      }
    }
  }
  return undefined;
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
  return "text";
}

function valueKindPlaceholder(
  propertySchema: JsonSchemaObject,
  texts: { path: string; literalOrPath: string }
): string | undefined {
  const valueKind = propertySchema["x-statevia-valueKind"];
  if (valueKind === "path") {
    return texts.path;
  }
  if (valueKind === "literalOrPath") {
    return texts.literalOrPath;
  }
  return undefined;
}
