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
      emailLabel: string;
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

  };
};
