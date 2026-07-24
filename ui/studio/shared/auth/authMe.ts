/** 認証済み Principal（`/api/auth/me`）。 */
export type AuthMeResponse = {
  tenantId: string;
  tenantKey: string;
  principalId: string;
  email: string;
  isTenantAdmin: boolean;
};
