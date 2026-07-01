/** 権限カタログ 1 件。 */
export type PermissionDefinitionDto = {
  permissionKey: string;
  displayLabel: string;
  displayKey?: string | null;
  isSystem: boolean;
  isDeprecated: boolean;
};

/** テナントユーザー一覧項目。 */
export type AdminUserListItem = {
  userId: string;
  principalId: string;
  email: string;
  displayName: string;
  isTenantAdmin: boolean;
  isActive: boolean;
  groupIds: string[];
  createdAt: string;
};

/** グループ一覧項目。 */
export type AdminGroupListItem = {
  groupId: string;
  name: string;
  isSystem: boolean;
  memberCount: number;
  permissionCount: number;
  updatedAt: string;
};

/** グループ詳細。 */
export type AdminGroupDetail = {
  groupId: string;
  name: string;
  isSystem: boolean;
  memberUserIds: string[];
  permissionKeys: string[];
};

/** API キー一覧項目（平文なし）。 */
export type AdminApiKeyListItem = {
  apiKeyId: string;
  name: string;
  keyPrefix: string;
  allowedScopes: string[];
  expiresAt: string | null;
  lastUsedAt: string | null;
  createdAt: string;
  isActive: boolean;
};

/** API キー作成応答（平文は一度だけ）。 */
export type CreatedAdminApiKey = {
  apiKeyId: string;
  name: string;
  keyPrefix: string;
  plainKey: string;
  allowedScopes: string[];
  expiresAt: string | null;
  createdAt: string;
};

/** 認証済み Principal（`/api/auth/me`）。 */
export type AuthMeResponse = {
  tenantId: string;
  tenantKey: string;
  principalId: string;
  email: string;
  isTenantAdmin: boolean;
};
