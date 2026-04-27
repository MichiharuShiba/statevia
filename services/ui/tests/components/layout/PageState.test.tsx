import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { PageState } from "../../../app/components/layout/PageState";
import { uiText } from "../../../app/lib/uiText";

describe("PageState", () => {
  it("loading 状態では output 要素として表示する", () => {
    // Arrange
    const props = { state: "loading" as const, message: "読込中" };

    // Act
    render(<PageState {...props} />);

    // Assert
    expect(screen.getByText(uiText.pageState.loading)).toBeInTheDocument();
    expect(screen.getByText("読込中").tagName).toBe("P");
    expect(screen.getByText(uiText.pageState.loading).closest("output")).not.toBeNull();
  });

  it("empty 状態では再試行ボタンを表示しない", () => {
    // Arrange
    const props = { state: "empty" as const, message: "対象なし" };

    // Act
    render(<PageState {...props} />);

    // Assert
    expect(screen.getByText(uiText.pageState.empty)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: uiText.actions.retry })).toBeNull();
  });

  it("error 状態では alert と再試行が機能する", () => {
    // Arrange
    const onRetry = vi.fn();
    render(<PageState state="error" message="失敗" onRetry={onRetry} />);
    const retryButton = screen.getByRole("button", { name: uiText.actions.retry });

    // Act
    fireEvent.click(retryButton);

    // Assert
    expect(screen.getByText(uiText.pageState.error)).toBeInTheDocument();
    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
