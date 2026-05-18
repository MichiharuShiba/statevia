import { describe, expect, it } from "vitest";
import { getUtf8ByteLength, isWithinMaxLength, matchesPattern } from "../../../app/lib/validation/primitives";

describe("validation/primitives", () => {
  it("isWithinMaxLength は文字数上限以内なら true", () => {
    expect(isWithinMaxLength("abc", 3)).toBe(true);
    expect(isWithinMaxLength("abcd", 3)).toBe(false);
    expect(isWithinMaxLength("", 0)).toBe(true);
  });

  it("getUtf8ByteLength は UTF-8 バイト数を返す", () => {
    expect(getUtf8ByteLength("a")).toBe(1);
    expect(getUtf8ByteLength("あ")).toBe(3);
  });

  it("matchesPattern は正規表現一致を返す", () => {
    expect(matchesPattern("evt-1", /^[a-z0-9-]+$/)).toBe(true);
    expect(matchesPattern("bad space", /^[a-z0-9-]+$/)).toBe(false);
  });
});
