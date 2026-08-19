import type { AuthFeatureUiText } from "./types";

/**
 * auth feature の英語辞書切片。
 */
export const authUiTextEn: AuthFeatureUiText = {
  auth: {
    login: {
      title: "Sign in",
      description: "Sign in with your tenant key and account credentials.",
      tenantKeyLabel: "Tenant key",
      usernameLabel: "Username",
      passwordLabel: "Password",
      showPassword: "Show password",
      hidePassword: "Hide password",
      submit: "Sign in",
      submitting: "Signing in…",
      sessionHint: "Or sign in at /login.",
    },
    errors: {
      network: "A network error occurred. Check your connection and try again.",
      unexpectedResponse: "The sign-in response was invalid. Contact your administrator.",
    },
    account: {
      title: "Account",
      description: "Confirm your current password, then update it.",
      currentPasswordLabel: "Current password",
      newPasswordLabel: "New password",
      passwordPolicyHint: "8–128 characters with no whitespace (symbols allowed; mixed case is not required)",
      confirmPasswordLabel: "Confirm new password",
      passwordMismatch: "The new passwords do not match.",
      submit: "Update",
      submitting: "Updating…",
      success: "Password updated.",
    },

  },
};
