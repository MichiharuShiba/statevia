import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { LanguageToggle } from "../../../app/components/layout/LanguageToggle";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: vi.fn() })
}));

describe("LanguageToggle", () => {
  it("言語切替で cookie を設定する", () => {
    render(<LanguageToggle locale="ja" />);

    fireEvent.click(screen.getByRole("button", { name: "EN" }));
    expect(document.cookie).toContain("ui-lang=en");
  });
});
