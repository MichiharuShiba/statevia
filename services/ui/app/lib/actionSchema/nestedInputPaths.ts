import type { JsonSchemaObject } from "./types";

/** スカラー入力フィールドまたはネスト object グループ。 */
export type InputFieldNode =
  | {
      kind: "scalar";
      logicalPath: string;
      propertySchema: JsonSchemaObject;
    }
  | {
      kind: "group";
      logicalPath: string;
      propertySchema: JsonSchemaObject;
      children: InputFieldNode[];
    };

/**
 * inputSchema からスカラー葉と object グループのツリーを構築する。
 */
export function buildInputFieldTree(
  inputSchema: JsonSchemaObject,
  fieldOrder?: string[] | null,
  logicalPathPrefix = ""
): InputFieldNode[] {
  const properties = inputSchema.properties ?? {};
  const propertyNames = listPropertyNames(properties, fieldOrder, logicalPathPrefix);
  const nodes: InputFieldNode[] = [];

  for (const propertyName of propertyNames) {
    const propertySchema = properties[propertyName];
    if (!propertySchema) {
      continue;
    }
    const logicalPath = logicalPathPrefix
      ? `${logicalPathPrefix}.${propertyName}`
      : propertyName;
    const nestedProperties = propertySchema.properties;
    if (propertySchema.type === "object" && nestedProperties && Object.keys(nestedProperties).length > 0) {
      nodes.push({
        kind: "group",
        logicalPath,
        propertySchema,
        children: buildInputFieldTree(propertySchema, null, logicalPath)
      });
      continue;
    }
    nodes.push({
      kind: "scalar",
      logicalPath,
      propertySchema
    });
  }

  return nodes;
}

/**
 * 論理パスでネストした値を取得する。
 */
export function getNestedInputValue(root: Record<string, unknown>, logicalPath: string): unknown {
  const parts = logicalPath.split(".");
  let current: unknown = root;
  for (const part of parts) {
    if (current === null || current === undefined || typeof current !== "object" || Array.isArray(current)) {
      return undefined;
    }
    current = (current as Record<string, unknown>)[part];
  }
  return current;
}

/**
 * 論理パスでネストした値を設定する（Playground 既定はネスト map 形式）。
 */
export function setNestedInputValue(
  root: Record<string, unknown>,
  logicalPath: string,
  value: unknown
): Record<string, unknown> {
  const parts = logicalPath.split(".");
  const result: Record<string, unknown> = { ...root };
  let current: Record<string, unknown> = result;

  for (let index = 0; index < parts.length - 1; index += 1) {
    const part = parts[index];
    const existing = current[part];
    const next =
      existing !== null && typeof existing === "object" && !Array.isArray(existing)
        ? { ...(existing as Record<string, unknown>) }
        : {};
    current[part] = next;
    current = next;
  }

  const leafKey = parts.at(-1);
  if (leafKey === undefined) {
    return result;
  }
  if (value === undefined || value === "") {
    delete current[leafKey];
    pruneEmptyObjects(result, parts);
  } else {
    current[leafKey] = value;
  }

  return result;
}

/**
 * `$.input.ship.address` 形式の jsonPath を論理パス `ship.address` に変換する。
 */
export function jsonPathToLogicalPath(jsonPath: string | undefined): string | undefined {
  if (!jsonPath) {
    return undefined;
  }
  const prefix = "$.input.";
  if (!jsonPath.startsWith(prefix)) {
    return undefined;
  }
  const remainder = jsonPath.slice(prefix.length);
  return remainder.length > 0 ? remainder : undefined;
}

function listPropertyNames(
  properties: Record<string, JsonSchemaObject>,
  fieldOrder: string[] | null | undefined,
  logicalPathPrefix: string
): string[] {
  const propertyNames = Object.keys(properties);
  if (!fieldOrder || fieldOrder.length === 0) {
    return propertyNames;
  }

  const prefix = logicalPathPrefix ? `${logicalPathPrefix}.` : "";
  const ordered = fieldOrder
    .filter((name) => name.startsWith(prefix))
    .map((name) => name.slice(prefix.length))
    .filter((name) => !name.includes(".") && propertyNames.includes(name));
  const remainder = propertyNames.filter((name) => !ordered.includes(name));
  return [...ordered, ...remainder];
}

function pruneEmptyObjects(root: Record<string, unknown>, parts: string[]): void {
  if (parts.length <= 1) {
    return;
  }

  const containers: Record<string, unknown>[] = [root];
  let current: Record<string, unknown> = root;
  for (let index = 0; index < parts.length - 1; index += 1) {
    const child = current[parts[index]];
    if (child === null || typeof child !== "object" || Array.isArray(child)) {
      return;
    }
    current = child as Record<string, unknown>;
    containers.push(current);
  }

  for (let index = containers.length - 1; index > 0; index -= 1) {
    const container = containers[index];
    if (Object.keys(container).length > 0) {
      break;
    }
    delete containers[index - 1][parts[index - 1]];
  }
}
