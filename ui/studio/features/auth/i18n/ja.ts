import type { AuthFeatureUiText } from "./types";

/**
 * auth feature の日本語辞書切片。
 */
export const authUiTextJa: AuthFeatureUiText = {
  auth: {
    login: {
      title: "ログイン",
      description: "テナントキーとアカウントでサインインしてください。",
      tenantKeyLabel: "テナントキー",
      usernameLabel: "ユーザー名",
      passwordLabel: "パスワード",
      showPassword: "パスワードを表示",
      hidePassword: "パスワードを非表示",
      submit: "ログイン",
      submitting: "ログイン中…",
      sessionHint: "または /login からサインインしてください。",
    },
    errors: {
      network: "ネットワークエラーが発生しました。接続を確認して再試行してください。",
      unexpectedResponse: "ログイン応答が不正です。管理者に連絡してください。",
    },
    account: {
      title: "アカウント",
      description: "現行パスワードを確認して、自分のパスワードを更新します。",
      currentPasswordLabel: "現行パスワード",
      newPasswordLabel: "新しいパスワード",
      passwordPolicyHint: "8〜128 文字（空白なし。記号可。大文字と小文字の混在は不要）",
      confirmPasswordLabel: "新しいパスワード（確認）",
      passwordMismatch: "新しいパスワードが一致しません。",
      submit: "更新",
      submitting: "更新中…",
      success: "パスワードを更新しました。",
    },

  },
};
