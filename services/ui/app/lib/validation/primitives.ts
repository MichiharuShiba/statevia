/**
 * 文字列長が上限以内かどうかを判定する。
 *
 * @param value 判定対象の文字列
 * @param maxLength 許可する最大文字数
 * @returns 上限以内なら true
 */
export function isWithinMaxLength(value: string, maxLength: number): boolean {
  return value.length <= maxLength;
}

/**
 * UTF-8 バイト長を返す。
 *
 * @param value 判定対象の文字列
 * @returns UTF-8 エンコード後のバイト数
 */
export function getUtf8ByteLength(value: string): number {
  return new TextEncoder().encode(value).length;
}

/**
 * 文字列が正規表現に一致するかどうかを判定する。
 *
 * @param value 判定対象の文字列
 * @param pattern 適用する正規表現
 * @returns 一致した場合に true
 */
export function matchesPattern(value: string, pattern: RegExp): boolean {
  return pattern.test(value);
}
