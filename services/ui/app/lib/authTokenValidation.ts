/**
 * JWT の exp 判定（Edge / ブラウザ向け。`Buffer` に依存しない）。
 */

import { JwtAccessToken } from "./jwtAccessToken";

export { JwtAccessToken, JwtSegment } from "./jwtAccessToken";
export type { JwtSegmentKind } from "./jwtAccessToken";

/**
 * JWT ペイロードの `exp`（Unix 秒）を読む。署名検証は行わない。
 * @param accessToken Bearer トークン文字列
 */
export function readJwtExpiryUnixSeconds(accessToken: string | undefined | null): number | null {
  return JwtAccessToken.parse(accessToken)?.readExpiryUnixSeconds() ?? null;
}

/**
 * アクセストークンが未期限切れか（Cookie 存在だけでは不十分な場合の判定）。
 */
export function isAccessTokenSessionValid(accessToken: string | undefined | null): boolean {
  const exp = readJwtExpiryUnixSeconds(accessToken);
  if (exp === null) {
    return false;
  }
  return exp > Math.floor(Date.now() / 1000);
}
