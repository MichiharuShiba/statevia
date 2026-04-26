import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { GraphLegend } from "../../../app/components/nodes/GraphLegend";
import { uiText } from "../../../app/lib/uiText";

describe("GraphLegend", () => {
  it("ノードステータス凡例の見出しを表示する", () => {
    render(<GraphLegend />);
    expect(screen.getByRole("heading", { name: uiText.graphLegend.heading.nodeStatus })).toBeInTheDocument();
  });

  it("エッジ種別凡例の見出しを表示する", () => {
    render(<GraphLegend />);
    expect(screen.getByRole("heading", { name: uiText.graphLegend.heading.edgeType })).toBeInTheDocument();
  });

  it("全ノードステータスを表示する", () => {
    render(<GraphLegend />);
    const statuses = ["IDLE", "READY", "RUNNING", "WAITING", "SUCCEEDED", "FAILED", "CANCELED"];
    for (const status of statuses) {
      expect(screen.getByText(status)).toBeInTheDocument();
    }
  });

  it("全エッジ種別を表示する", () => {
    render(<GraphLegend />);
    expect(screen.getByText(uiText.status.edgeTypeNext)).toBeInTheDocument();
    expect(screen.getByText(uiText.status.edgeTypeResume)).toBeInTheDocument();
    expect(screen.getByText(uiText.status.edgeTypeCancel)).toBeInTheDocument();
  });

  it("グラフ凡例としてアクセシブルである", () => {
    render(<GraphLegend />);
    expect(screen.getByRole("region", { name: uiText.graphLegend.aria.root })).toBeInTheDocument();
  });
});
