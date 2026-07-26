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
      emailLabel: "メールアドレス",
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

  },
};
