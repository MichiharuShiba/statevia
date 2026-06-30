/** Action Schema API の型定義（Core-API `/v1/actions/schema` 準拠）。 */

/** `/v1/actions/schema/index` のレスポンス（登録 action 一覧）。 */
export type ActionSchemaIndexResponse = {
  items: ActionSchemaIndexItem[];
};

/** action 一覧の 1 要素（識別子・表示名・バージョン）。 */
export type ActionSchemaIndexItem = {
  actionId: string;
  displayName: string;
  version: string;
};

/** `/v1/actions/schema/{actionId}` のレスポンス（descriptor + schema + UI メタ）。 */
export type ActionSchemaDetailResponse = {
  descriptor: ActionSchemaDescriptor;
  schema: ActionSchemaBundle;
  uiMetadata?: ActionUiMetadata | null;
};

/** action のメタ情報（識別子・バージョン・表示名・説明・カテゴリ）。 */
export type ActionSchemaDescriptor = {
  actionId: string;
  version: string;
  displayName: string;
  description?: string | null;
  category?: string | null;
};

/** action の入出力 JSON Schema バンドル。 */
export type ActionSchemaBundle = {
  inputSchema: JsonSchemaObject;
  outputSchema: JsonSchemaObject;
  schemaVersion: string;
};

/** Playground フォーム生成に用いる UI メタ（フィールド順・ヒント・enum ラベル）。 */
export type ActionUiMetadata = {
  fieldOrder?: string[] | null;
  fields?: Record<string, ActionFieldUiHints> | null;
  enumLabelKeys?: Record<string, string> | null;
};

/** 個々のフィールドの UI ヒント（widget・ラベルキー・機微フラグなど）。 */
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

/** 422 応答に含まれる action input 検証エラーの詳細。 */
export type ActionInputValidationDetail = {
  message?: string;
  state?: string;
  actionId?: string;
  jsonPath?: string;
};
