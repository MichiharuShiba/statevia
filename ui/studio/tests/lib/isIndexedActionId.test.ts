import { describe, expect, it } from "vitest";
import {
  buildIndexedActionIdSet,
  isIndexedActionId
} from "@/features/definition-editor/actionSchema/isIndexedActionId";
import type { ActionSchemaIndexItem } from "@/features/definition-editor/actionSchema/types";

const candidates: ActionSchemaIndexItem[] = [
  {
    actionId: "statevia.action.reference.http.request",
    displayName: "REST",
    version: "1.0.0"
  }
];

describe("isIndexedActionId", () => {
  it("index 候補に含まれる actionId のみ true を返す", () => {
    expect(isIndexedActionId("statevia.action.reference.http.request", candidates)).toBe(true);
    expect(isIndexedActionId("sleep", candidates)).toBe(false);
    expect(isIndexedActionId("noop", candidates)).toBe(false);
  });

  it("buildIndexedActionIdSet が actionId 集合を構築する", () => {
    expect(buildIndexedActionIdSet(candidates).has("statevia.action.reference.http.request")).toBe(true);
    expect(buildIndexedActionIdSet(candidates).has("sleep")).toBe(false);
  });
});
