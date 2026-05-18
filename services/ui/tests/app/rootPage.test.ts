import { describe, expect, it, vi } from "vitest";

const redirect = vi.fn();

vi.mock("next/navigation", () => ({
  redirect
}));

describe("RootPage", () => {
  it("/ から /dashboard へリダイレクトする", async () => {
    const { default: RootPage } = await import("../../app/page");
    RootPage();
    expect(redirect).toHaveBeenCalledWith("/dashboard");
  });
});
