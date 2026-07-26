/**
 * Definition 名に許可する文字種（先頭は英字、以降は半角英数字・`.`・`_`・`-`）。
 */
export const DEFINITION_NAME_PATTERN = /^[A-Za-z][A-Za-z0-9._-]{0,99}$/;

/**
 * Definition 名の最大文字数。
 */
export const DEFINITION_NAME_MAX_LENGTH = 100;

/**
 * 実行イベント名に許可する文字種（先頭は英字、以降は半角英数字・`.`・`_`・`-`）。
 */
export const EVENT_NAME_PATTERN = /^[A-Za-z][A-Za-z0-9._-]*$/;

/**
 * 実行イベント名の最大文字数。
 */
export const EVENT_NAME_MAX_LENGTH = 64;

/**
 * 実行開始時 `input` JSON の最大 UTF-8 バイト数（64KB）。
 */
export const START_INPUT_MAX_BYTES = 65536;

/**
 * Definition YAML の最大 UTF-8 バイト数（256KB）。
 */
export const DEFINITION_YAML_MAX_BYTES = 256 * 1024;
