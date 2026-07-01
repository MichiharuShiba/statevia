/**
 * テスト用 JWT（署名検証なし。3 セグメントとも Base64URL として妥当）。
 * @param exp Unix 秒の有効期限
 */
export function testJwt(exp: number): string {
  const header = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
  const payload = Buffer.from(JSON.stringify({ exp })).toString("base64url");
  const signature = Buffer.from("sig-bytes").toString("base64url");
  return `${header}.${payload}.${signature}`;
}
