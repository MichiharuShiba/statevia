import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PageShell } from "../../../app/components/layout/PageShell";

describe("PageShell", () => {
  it("title と description を表示する", () => {
    // Arrange
    const props = {
      title: "Workflow 一覧",
      description: "一覧画面です。",
      children: <div>body</div>
    };

    // Act
    render(<PageShell {...props} />);

    // Assert
    expect(screen.getByRole("heading", { name: "Workflow 一覧" })).toBeInTheDocument();
    expect(screen.getByText("一覧画面です。")).toBeInTheDocument();
    expect(screen.getByText("body")).toBeInTheDocument();
  });

  it("primaryActions と secondaryActions を表示する", () => {
    // Arrange
    render(
      <PageShell
        title="編集"
        primaryActions={<button type="button">保存</button>}
        secondaryActions={<a href="/definitions">一覧に戻る</a>}
      >
        <div>content</div>
      </PageShell>
    );

    // Act
    const saveButton = screen.getByRole("button", { name: "保存" });

    // Assert
    expect(saveButton).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "一覧に戻る" })).toBeInTheDocument();
  });
});
