import { describe, expect, it } from "vitest";
import { filterActionSchemaCandidates } from "../../app/lib/actionSchema/filterActionSchemaCandidates";
import type { ActionSchemaIndexItem } from "../../app/lib/actionSchema/types";

const candidates: ActionSchemaIndexItem[] = [
  {
    actionId: "statevia.action.builtin.rest",
    displayName: "REST",
    version: "1.0.0"
  },
  {
    actionId: "statevia.action.builtin.noop",
    displayName: "No-op",
    version: "1.0.0"
  },
  {
    actionId: "statevia.action.builtin.sleep",
    displayName: "Sleep",
    version: "1.0.0"
  }
];

describe("filterActionSchemaCandidates", () => {
  it("クエリ空のときは全件を返す", () => {
    expect(filterActionSchemaCandidates(candidates, "")).toHaveLength(3);
  });

  it("actionId / displayName で部分一致絞り込みする", () => {
    expect(filterActionSchemaCandidates(candidates, "rest").map((item) => item.actionId)).toEqual([
      "statevia.action.builtin.rest"
    ]);
    expect(filterActionSchemaCandidates(candidates, "no-op").map((item) => item.actionId)).toEqual([
      "statevia.action.builtin.noop"
    ]);
  });

  it("絞り込み結果は上限件数で打ち切る", () => {
    const many = Array.from({ length: 80 }, (_, index) => ({
      actionId: `statevia.action.builtin.item${index}`,
      displayName: `Item ${index}`,
      version: "1.0.0"
    }));
    expect(filterActionSchemaCandidates(many, "item", 20)).toHaveLength(20);
  });
});
