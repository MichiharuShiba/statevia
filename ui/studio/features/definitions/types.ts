/**
 * Definitions feature のドメイン型（Service API `DefinitionResponse` 相当）。
 */

/** GET /v1/definitions の要素。 */
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
   * `includeDeleted=true` の一覧でのみ truthy になりうる。通常 GET / restore 成功では含めない。
   */
  deletedAt?: string | null;
};

/** ページング付き定義一覧。 */
export type PagedDefinitions = {
  items: DefinitionDTO[];
  totalCount: number;
  offset: number;
  limit: number;
  hasMore: boolean;
};

/**
 * 定義が catalog 上削除済みかどうかを判定する。
 * @param definition 一覧・詳細の要素
 * @returns `deletedAt` が truthy のとき true
 */
export function isDeletedDefinition(definition: DefinitionDTO): boolean {
  return Boolean(definition.deletedAt);
}
