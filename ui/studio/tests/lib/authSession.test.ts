import { afterEach, describe, expect, it } from "vitest";
import {
  cookieMaxAgeSeconds,
  hasServerDevAuthBypass,
  isAccessTokenSessionValid,
  readJwtExpiryUnixSeconds
} from "@/shared/auth/authSession";
import { testJwt } from "../helpers/testJwt";

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

describe("hasServerDevAuthBypass", () => {
  const originalEnv = process.env;

  afterEach(() => {
    process.env = originalEnv;
  });

  it("development では SERVICE_API_AUTH_TOKEN があると true", () => {
    // Arrange
    process.env = { ...originalEnv, NODE_ENV: "development", SERVICE_API_AUTH_TOKEN: "dev-token" };

    // Act
    const bypass = hasServerDevAuthBypass();

    // Assert
    expect(bypass).toBe(true);
  });

  it("production では SERVICE_API_AUTH_TOKEN があっても false", () => {
    // Arrange
    process.env = { ...originalEnv, NODE_ENV: "production", SERVICE_API_AUTH_TOKEN: "dev-token" };

    // Act
    const bypass = hasServerDevAuthBypass();

    // Assert
    expect(bypass).toBe(false);
  });
});
