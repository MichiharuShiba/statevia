/**
 * ログインユーザー名の最大長（Service API / DB と一致）。
 */
export const USERNAME_MAX_LENGTH = 64;

/**
 * 連絡先メールの最大長（Service API / DB と一致）。
 */
export const USER_EMAIL_MAX_LENGTH = 256;

/**
 * ユーザー名の許可パターン。先頭・末尾は英数字。途中のみハイフン・アンダースコア・ドット。
 */
export const USERNAME_PATTERN = "^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$";
