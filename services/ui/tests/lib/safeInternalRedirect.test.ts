import { describe, expect, it } from "vitest";
import {
  DEFAULT_POST_LOGIN_PATH,
  normalizeInternalRedirectPath,
  resolveSafeInternalRedirectPath
} from "../../app/lib/safeInternalRedirect";

describe("normalizeInternalRedirectPath", () => {
  it("内部パスとクエリを正規化して返す", () => {
    expect(normalizeInternalRedirectPath("/definitions")).toBe("/definitions");
    expect(normalizeInternalRedirectPath("/definitions?tab=1")).toBe("/definitions?tab=1");
  });

  it("外部 URL・プロトコル相対・ログイン系は拒否する", () => {
    expect(normalizeInternalRedirectPath("https://evil.com")).toBeNull();
    expect(normalizeInternalRedirectPath("//evil.com")).toBeNull();
    expect(normalizeInternalRedirectPath("/login")).toBeNull();
    expect(normalizeInternalRedirectPath("/login/")).toBeNull();
    expect(normalizeInternalRedirectPath("/login?x=1")).toBeNull();
  });

  it("バックスラッシュ・制御文字・パス内コロンは拒否する", () => {
    expect(normalizeInternalRedirectPath(String.raw`/\evil.com`)).toBeNull();
    expect(normalizeInternalRedirectPath("/foo\n/bar")).toBeNull();
    expect(normalizeInternalRedirectPath("/http://evil.com")).toBeNull();
  });

  it("デコード後に // になるパスは拒否する", () => {
    expect(normalizeInternalRedirectPath("/%2F%2Fevil.com")).toBeNull();
  });
});

describe("resolveSafeInternalRedirectPath", () => {
  it("不正な from は既定の dashboard にフォールバックする", () => {
    expect(resolveSafeInternalRedirectPath(null)).toBe(DEFAULT_POST_LOGIN_PATH);
    expect(resolveSafeInternalRedirectPath("//evil.com")).toBe(DEFAULT_POST_LOGIN_PATH);
  });
});
