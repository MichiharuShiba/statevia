/** Action Schema API の型定義（Core-API `/v1/actions/schema` 準拠）。 */

export type ActionSchemaIndexResponse = {
  items: ActionSchemaIndexItem[];
};

export type ActionSchemaIndexItem = {
  actionId: string;
  displayName: string;
  version: string;
};

export type ActionSchemaDetailResponse = {
  descriptor: ActionSchemaDescriptor;
  schema: ActionSchemaBundle;
  uiMetadata?: ActionUiMetadata | null;
};

export type ActionSchemaDescriptor = {
  actionId: string;
  version: string;
  displayName: string;
  description?: string | null;
  category?: string | null;
};

export type ActionSchemaBundle = {
  inputSchema: JsonSchemaObject;
  outputSchema: JsonSchemaObject;
  schemaVersion: string;
};

export type ActionUiMetadata = {
  fieldOrder?: string[] | null;
  fields?: Record<string, ActionFieldUiHints> | null;
  enumLabelKeys?: Record<string, string> | null;
};

export type ActionFieldUiHints = {
  widget?: string | null;
  labelKey?: string | null;
  descriptionKey?: string | null;
  placeholderKey?: string | null;
  sensitive?: boolean;
};

/** JSON Schema の object ノード（Playground フォーム生成用の最小表現）。 */
export type JsonSchemaObject = {
  type?: string;
  title?: string;
  description?: string;
  enum?: unknown[];
  format?: string;
  properties?: Record<string, JsonSchemaObject>;
  required?: string[];
  additionalProperties?: boolean | JsonSchemaObject;
  oneOf?: JsonSchemaObject[];
  minimum?: number;
  default?: unknown;
  "x-statevia-valueKind"?: string;
};

export type ActionInputValidationDetail = {
  message?: string;
  state?: string;
  actionId?: string;
  jsonPath?: string;
};
