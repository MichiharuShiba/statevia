import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { GraphLegend } from "../../../app/components/nodes/GraphLegend";

describe("GraphLegend", () => {
  it("ノードステータス凡例の見出しを表示する", () => {
    render(<GraphLegend />);
    expect(screen.getByRole("heading", { name: /ノードステータス/ })).toBeInTheDocument();
  });

  it("エッジ種別凡例の見出しを表示する", () => {
    render(<GraphLegend />);
    expect(screen.getByRole("heading", { name: /エッジ種別/ })).toBeInTheDocument();
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
    expect(screen.getByText("Next")).toBeInTheDocument();
    expect(screen.getByText("Resume")).toBeInTheDocument();
    expect(screen.getByText("Cancel")).toBeInTheDocument();
  });

  it("グラフ凡例としてアクセシブルである", () => {
    render(<GraphLegend />);
    expect(screen.getByRole("region", { name: "グラフ凡例" })).toBeInTheDocument();
  });
});
