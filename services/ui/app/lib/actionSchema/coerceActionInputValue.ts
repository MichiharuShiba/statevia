import type { JsonSchemaObject } from "./types";

/** schema 強制後のスカラー値（未変換は undefined）。 */
type CoercedScalarValue = string | number | boolean | undefined;

/**
 * JSONPath 式（`$.` 始まり）かどうかを判定する。
 */
export function isJsonPathExpression(text: string): boolean {
  return text.trimStart().startsWith("$.");
}

/**
 * フォーム入力文字列を schema に合わせたリテラル値へ正規化する。
 * `literalOrPath` では path は文字列のまま、数値のみの入力は integer/number に変換する。
 */
export function coerceScalarForSchema(raw: string, propertySchema: JsonSchemaObject): CoercedScalarValue {
  const trimmed = raw.trim();
  if (!trimmed) {
    return undefined;
  }
  if (isJsonPathExpression(trimmed)) {
    return trimmed;
  }

  const valueKind = propertySchema["x-statevia-valueKind"];
  if (valueKind === "path") {
    return trimmed;
  }

  if (propertySchema.oneOf && propertySchema.oneOf.length > 0) {
    for (const branch of propertySchema.oneOf) {
      const coerced = tryCoerceForSingleSchema(trimmed, branch);
      if (coerced !== undefined) {
        return coerced;
      }
    }
    return trimmed;
  }

  return tryCoerceForSingleSchema(trimmed, propertySchema) ?? trimmed;
}

/**
 * 既存 input マップのスカラー値を schema に合わせて正規化する（YAML 由来の文字列数値を含む）。
 */
export function normalizeActionInputRecord(
  value: Record<string, unknown>,
  inputSchema: JsonSchemaObject,
  fieldNames: readonly string[]
): Record<string, unknown> {
  const properties = inputSchema.properties ?? {};
  const result = { ...value };
  for (const fieldName of fieldNames) {
    const fieldValue = result[fieldName];
    if (typeof fieldValue !== "string") {
      continue;
    }
    const propertySchema = properties[fieldName];
    if (!propertySchema) {
      continue;
    }
    const coerced = coerceScalarForSchema(fieldValue, propertySchema);
    if (coerced !== fieldValue) {
      result[fieldName] = coerced;
    }
  }
  return result;
}

function tryCoerceForSingleSchema(trimmed: string, propertySchema: JsonSchemaObject): CoercedScalarValue {
  switch (propertySchema.type) {
    case "integer":
      if (/^-?\d+$/.test(trimmed)) {
        return Number.parseInt(trimmed, 10);
      }
      return undefined;
    case "number":
      if (/^-?\d+(\.\d+)?$/.test(trimmed)) {
        return Number.parseFloat(trimmed);
      }
      return undefined;
    case "boolean":
      if (trimmed === "true") {
        return true;
      }
      if (trimmed === "false") {
        return false;
      }
      return undefined;
    case "string":
      return trimmed;
    default:
      return undefined;
  }
}
