import { describe, expect, it } from "vitest";
import { filterActionSchemaCandidates } from "@/features/definition-editor/actionSchema/filterActionSchemaCandidates";
import type { ActionSchemaIndexItem } from "@/features/definition-editor/actionSchema/types";

const candidates: ActionSchemaIndexItem[] = [
  {
    actionId: "statevia.action.reference.http.request",
    displayName: "REST",
    version: "1.0.0"
  },
  {
    actionId: "statevia.action.builtin.execution.noop",
    displayName: "No-op",
    version: "1.0.0"
  },
  {
    actionId: "statevia.action.builtin.execution.sleep",
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
      "statevia.action.reference.http.request"
    ]);
    expect(filterActionSchemaCandidates(candidates, "no-op").map((item) => item.actionId)).toEqual([
      "statevia.action.builtin.execution.noop"
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
