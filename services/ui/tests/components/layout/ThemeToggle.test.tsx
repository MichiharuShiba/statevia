import { describe, expect, it } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { ThemeToggle } from "../../../app/components/layout/ThemeToggle";

describe("ThemeToggle", () => {
  it("テーマ切替で data-theme と cookie を更新する", () => {
    render(<ThemeToggle theme="light" />);

    fireEvent.click(screen.getByRole("button", { name: "Dark" }));
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(document.cookie).toContain("ui-theme=dark");
  });
});
