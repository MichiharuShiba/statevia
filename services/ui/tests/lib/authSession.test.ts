import { describe, expect, it } from "vitest";
import {
  cookieMaxAgeSeconds,
  isAccessTokenSessionValid,
  readJwtExpiryUnixSeconds
} from "../../app/lib/authSession";

function testJwt(exp: number): string {
  const header = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
  const payload = Buffer.from(JSON.stringify({ exp })).toString("base64url");
  return `${header}.${payload}.signature`;
}

describe("cookieMaxAgeSeconds", () => {
  it("有効期限までの秒数を返す（最低 60 秒）", () => {
    const expiresAt = new Date(Date.now() + 120_000).toISOString();
    expect(cookieMaxAgeSeconds(expiresAt)).toBeGreaterThanOrEqual(60);
    expect(cookieMaxAgeSeconds(expiresAt)).toBeLessThanOrEqual(120);
  });

  it("過去の有効期限でも最低 60 秒を返す", () => {
    const expiresAt = new Date(Date.now() - 60_000).toISOString();
    expect(cookieMaxAgeSeconds(expiresAt)).toBe(60);
  });
});

describe("isAccessTokenSessionValid", () => {
  it("exp が未来の JWT は有効", () => {
    const token = testJwt(Math.floor(Date.now() / 1000) + 3600);
    expect(isAccessTokenSessionValid(token)).toBe(true);
    expect(readJwtExpiryUnixSeconds(token)).toBeGreaterThan(Math.floor(Date.now() / 1000));
  });

  it("exp が過去の JWT は無効", () => {
    const token = testJwt(Math.floor(Date.now() / 1000) - 60);
    expect(isAccessTokenSessionValid(token)).toBe(false);
  });

  it("不正なトークンは無効", () => {
    expect(isAccessTokenSessionValid("not-a-jwt")).toBe(false);
    expect(isAccessTokenSessionValid(null)).toBe(false);
  });
});
