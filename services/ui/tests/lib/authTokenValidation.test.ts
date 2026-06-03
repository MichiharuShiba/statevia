import { describe, expect, it } from "vitest";
import {
  isAccessTokenSessionValid,
  readJwtExpiryUnixSeconds
} from "../../app/lib/authTokenValidation";
import { testJwt } from "../helpers/testJwt";

describe("authTokenValidation", () => {
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
    expect(isAccessTokenSessionValid("invalid")).toBe(false);
    expect(readJwtExpiryUnixSeconds(null)).toBeNull();
  });

  it("exp が数値でない JWT は無効", () => {
    const header = Buffer.from(JSON.stringify({ alg: "HS256" })).toString("base64url");
    const payload = Buffer.from(JSON.stringify({ exp: "not-a-number" })).toString("base64url");
    const signature = Buffer.from("sig-bytes").toString("base64url");
    const token = `${header}.${payload}.${signature}`;

    expect(readJwtExpiryUnixSeconds(token)).toBeNull();
    expect(isAccessTokenSessionValid(token)).toBe(false);
  });
});
