import { describe, expect, it } from "vitest";
import {
  buildIndexedActionIdSet,
  isIndexedActionId
} from "../../app/lib/actionSchema/isIndexedActionId";
import type { ActionSchemaIndexItem } from "../../app/lib/actionSchema/types";

const candidates: ActionSchemaIndexItem[] = [
  {
    actionId: "statevia.action.builtin.rest",
    displayName: "REST",
    version: "1.0.0"
  }
];

describe("isIndexedActionId", () => {
  it("index 候補に含まれる actionId のみ true を返す", () => {
    expect(isIndexedActionId("statevia.action.builtin.rest", candidates)).toBe(true);
    expect(isIndexedActionId("sleep", candidates)).toBe(false);
    expect(isIndexedActionId("noop", candidates)).toBe(false);
  });

  it("buildIndexedActionIdSet が actionId 集合を構築する", () => {
    expect(buildIndexedActionIdSet(candidates).has("statevia.action.builtin.rest")).toBe(true);
    expect(buildIndexedActionIdSet(candidates).has("sleep")).toBe(false);
  });
});
