/**
 * 一覧検索の `name` 入力で許可する文字種（半角英数字・`.`・`_`・`-`）。
 */
export const SEARCH_NAME_PATTERN = /^[A-Za-z0-9._-]{0,100}$/;

/**
 * 一覧検索の `name` 入力の最大文字数。
 */
export const SEARCH_NAME_MAX_LENGTH = 100;

/**
 * 実行一覧の `definitionId` フィルタで許可する文字種。
 */
export const DEFINITION_ID_PATTERN = /^[A-Za-z0-9_-]{0,80}$/;

/**
 * 実行一覧の `definitionId` フィルタの最大文字数。
 */
export const DEFINITION_ID_MAX_LENGTH = 80;
