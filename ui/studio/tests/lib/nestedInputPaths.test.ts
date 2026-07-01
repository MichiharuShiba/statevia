import { describe, expect, it } from "vitest";
import {
  buildInputFieldTree,
  getNestedInputValue,
  jsonPathToLogicalPath,
  setNestedInputValue
} from "../../app/lib/actionSchema/nestedInputPaths";

describe("nestedInputPaths", () => {
  it("object property をネストグループとして列挙する", () => {
    const tree = buildInputFieldTree({
      type: "object",
      properties: {
        ship: {
          type: "object",
          properties: {
            address: { type: "string" },
            contact: {
              type: "object",
              properties: {
                email: { type: "string", format: "email" }
              }
            }
          }
        }
      }
    });

    expect(tree).toHaveLength(1);
    expect(tree[0].kind).toBe("group");
    if (tree[0].kind === "group") {
      expect(tree[0].logicalPath).toBe("ship");
      expect(tree[0].children.map((child) => child.logicalPath)).toEqual([
        "ship.address",
        "ship.contact"
      ]);
    }
  });

  it("ネスト map 形式で値の読み書きをする", () => {
    const root: Record<string, unknown> = {};
    const next = setNestedInputValue(root, "ship.contact.email", "a@example.com");
    expect(getNestedInputValue(next, "ship.contact.email")).toBe("a@example.com");
    expect(next).toEqual({ ship: { contact: { email: "a@example.com" } } });
  });

  it("jsonPath を論理パスへ変換する", () => {
    expect(jsonPathToLogicalPath("$.input.ship.address")).toBe("ship.address");
  });
});
