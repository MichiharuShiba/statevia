/**
 * admin feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type AdminFeatureUiText = {
  admin: {
    users: {
      title: string;
      description: string;
      createTitle: string;
      emailLabel: string;
      passwordLabel: string;
      displayNameLabel: string;
      isTenantAdminLabel: string;
      createSubmit: string;
      creating: string;
      active: string;
      inactive: string;
      disable: string;
      enable: string;
      adminBadge: string;
      groupCount: (count: number) => string;
      forbidden: string;
    };
    groupManagement: {
      title: string;
      description: string;
      createTitle: string;
      nameLabel: string;
      createSubmit: string;
      creating: string;
      membersTitle: string;
      permissionsTitle: string;
      saveMembers: string;
      savePermissions: string;
      saving: string;
      systemBadge: string;
      memberCount: (count: number) => string;
      permissionCount: (count: number) => string;
      openDetail: string;
      backToList: string;
      forbidden: string;
    };
    permissions: {
      label: (displayKey: string | null | undefined, displayLabel: string) => string;
    };
    apiKeys: {
      title: string;
      description: string;
      createTitle: string;
      nameLabel: string;
      scopesTitle: string;
      expiresAtLabel: string;
      expiresAtHint: string;
      createSubmit: string;
      creating: string;
      active: string;
      inactive: string;
      revoke: string;
      revoking: string;
      prefixLabel: (prefix: string) => string;
      lastUsedLabel: (value: string) => string;
      neverUsed: string;
      createdLabel: (value: string) => string;
      expiresLabel: (value: string) => string;
      noExpiry: string;
      issuedTitle: string;
      issuedWarning: string;
      plainKeyLabel: string;
      copyKey: string;
      copiedKey: string;
      dismissIssued: string;
      forbidden: string;
    };

  };
};
