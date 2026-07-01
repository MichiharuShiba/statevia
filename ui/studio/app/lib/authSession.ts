/** httpOnly: JWT アクセストークン（Core-API プロキシが Bearer に載せる）。 */
export const AUTH_COOKIE_ACCESS = "statevia-access-token";

/** httpOnly: 外部テナントキー（Core-API プロキシが X-Tenant-Id に載せる）。 */
export const AUTH_COOKIE_TENANT_KEY = "statevia-tenant-key";

/** Core-API `LoginResponse`（camelCase JSON）。 */
export type LoginResponseBody = {
  accessToken: string;
  expiresAt: string;
  tenantId: string;
  tenantKey: string;
  principalId: string;
};

/** UI ログイン API 要求 body。 */
export type LoginRequestBody = {
  tenantKey: string;
  email: string;
  password: string;
};

/**
 * JWT 有効期限から Cookie `maxAge`（秒）を算出する。
 * @param expiresAtUtc ISO 8601（API の ExpiresAt）
 */
export function cookieMaxAgeSeconds(expiresAtUtc: string): number {
  const expiresMs = new Date(expiresAtUtc).getTime();
  const deltaSec = Math.floor((expiresMs - Date.now()) / 1000);
  return Math.max(60, deltaSec);
}

/**
 * 開発用: サーバー側プロキシが Bearer を環境変数から付与するか。
 */
export function hasServerDevAuthBypass(): boolean {
  return Boolean(process.env.CORE_API_AUTH_TOKEN?.trim());
}

export {
  isAccessTokenSessionValid,
  readJwtExpiryUnixSeconds
} from "./authTokenValidation";
