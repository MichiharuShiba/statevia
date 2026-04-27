import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { StatusBadge } from "../../../app/components/common/StatusBadge";

describe("StatusBadge", () => {
  it("Workflow ステータスを表示する", () => {
    // Arrange
    const props = { status: "Running" as const };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("Running")).toBeInTheDocument();
  });

  it("追加 className を結合して表示する", () => {
    // Arrange
    const props = { status: "Completed" as const, className: "rounded-full" };

    // Act
    render(<StatusBadge {...props} />);

    // Assert
    expect(screen.getByText("Completed")).toHaveClass("rounded-full");
  });
});
