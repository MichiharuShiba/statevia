import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { ReplayBanner } from "../../../app/components/execution/ReplayBanner";
import { renderWithUiText } from "../../testUtils";

describe("ReplayBanner", () => {
  it("現在に戻るボタンでコールバックを呼ぶ", () => {
    const onBackToCurrent = vi.fn();
    renderWithUiText(<ReplayBanner onBackToCurrent={onBackToCurrent} />);

    fireEvent.click(screen.getByRole("button"));
    expect(onBackToCurrent).toHaveBeenCalledTimes(1);
  });
});
