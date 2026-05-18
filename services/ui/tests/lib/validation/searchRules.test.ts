import { describe, expect, it } from "vitest";
import {
  DEFINITION_ID_MAX_LENGTH,
  DEFINITION_ID_PATTERN,
  SEARCH_NAME_MAX_LENGTH,
  SEARCH_NAME_PATTERN
} from "../../../app/lib/validation/searchRules";

describe("searchRules", () => {
  it("SEARCH_NAME_PATTERN は許可文字のみ受け入れる", () => {
    expect(SEARCH_NAME_PATTERN.test("demo-1_test.v2")).toBe(true);
    expect(SEARCH_NAME_PATTERN.test("invalid space")).toBe(false);
    expect(SEARCH_NAME_MAX_LENGTH).toBe(100);
  });

  it("DEFINITION_ID_PATTERN は英数字とハイフン・アンダースコアのみ", () => {
    expect(DEFINITION_ID_PATTERN.test("def-01")).toBe(true);
    expect(DEFINITION_ID_PATTERN.test("bad.id")).toBe(false);
    expect(DEFINITION_ID_MAX_LENGTH).toBe(80);
  });
});
