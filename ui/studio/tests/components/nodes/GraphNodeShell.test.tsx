import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { GraphNodeShell } from "../../../app/components/nodes/GraphNodeShell";

describe("GraphNodeShell", () => {
  it("アクション形状で子要素を描画する", () => {
    render(
      <GraphNodeShell shapeKind="roundedRect" borderClass="border" bgClass="bg" selected>
        <span>node-body</span>
      </GraphNodeShell>
    );

    expect(screen.getByText("node-body")).toBeInTheDocument();
  });
});
