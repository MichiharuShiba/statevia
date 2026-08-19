/**
 * auth feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type AuthFeatureUiText = {
  auth: {
    login: {
      title: string;
      description: string;
      tenantKeyLabel: string;
      usernameLabel: string;
      passwordLabel: string;
      showPassword: string;
      hidePassword: string;
      submit: string;
      submitting: string;
      sessionHint: string;
    };
    errors: {
      network: string;
      unexpectedResponse: string;
    };
    account: {
      title: string;
      description: string;
      currentPasswordLabel: string;
      newPasswordLabel: string;
      passwordPolicyHint: string;
      confirmPasswordLabel: string;
      passwordMismatch: string;
      submit: string;
      submitting: string;
      success: string;
    };

  };
};
