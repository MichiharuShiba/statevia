/**
 * Definition Editor feature が扱う API 応答形。
 * catalog 一覧の正本は `features/definitions/types`。フィールドは Core-API 契約に合わせる。
 */

/** GET/PUT/POST /definitions の要素（編集画面用）。 */
export type DefinitionDTO = {
  displayId: string;
  resourceId: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  yaml?: string;
  /** 最新版番号（API が返す場合）。 */
  latestVersion?: number;
  /**
   * catalog 論理削除日時（UTC）。
   * 編集画面の通常 GET では含めない想定。
   */
  deletedAt?: string | null;
};

/** 定義スキーマ取得 API（`/definitions/schema/nodes`）のレスポンス。 */
export type DefinitionSchemaResponse = {
  schemaVersion: string;
  nodesVersion: number;
  schema: Record<string, unknown>;
};
