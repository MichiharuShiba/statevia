/**
 * ログインユーザー名の最大長（Service API / DB と一致）。
 */
export const USERNAME_MAX_LENGTH = 64;

/**
 * 連絡先メールの最大長（Service API / DB と一致）。
 */
export const USER_EMAIL_MAX_LENGTH = 256;

/**
 * 新規・更新パスワードの最短長（Service API と一致）。
 */
export const PASSWORD_MIN_LENGTH = 8;

/**
 * 新規・更新パスワードの最長長（Service API と一致）。
 */
export const PASSWORD_MAX_LENGTH = 128;

/**
 * 新規・更新パスワードの許可パターン。空白以外。記号可。大文字小文字の混在は不要。
 */
export const PASSWORD_PATTERN = String.raw`^\S+$`;

/**
 * ユーザー名の許可パターン。先頭・末尾は英数字。途中のみハイフン・アンダースコア・ドット。
 */
export const USERNAME_PATTERN = "^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$";
