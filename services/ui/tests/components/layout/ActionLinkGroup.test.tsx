import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ActionLinkGroup } from "../../../app/components/layout/ActionLinkGroup";

describe("ActionLinkGroup", () => {
  it("links が空のときは何も描画しない", () => {
    // Arrange
    const props = { links: [] };

    // Act
    const { container } = render(<ActionLinkGroup {...props} />);

    // Assert
    expect(container.firstChild).toBeNull();
  });

  it("primary と secondary のリンクを描画する", () => {
    // Arrange
    const props = {
      links: [
        { label: "詳細", href: "/workflows/1", priority: "primary" as const },
        { label: "一覧", href: "/workflows" }
      ]
    };

    // Act
    render(<ActionLinkGroup {...props} />);

    // Assert
    expect(screen.getByRole("link", { name: "詳細" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "一覧" })).toBeInTheDocument();
  });
});
