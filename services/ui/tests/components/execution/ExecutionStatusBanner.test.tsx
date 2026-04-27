import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ExecutionStatusBanner } from "../../../app/components/execution/ExecutionStatusBanner";
import { uiText } from "../../../app/lib/uiText";

describe("ExecutionStatusBanner", () => {
  it("cancelRequested と terminal がともに falsy のとき null を返す", () => {
    // Arrange
    const props = { cancelRequested: false, terminal: false };

    // Act
    const { container } = render(<ExecutionStatusBanner {...props} />);

    // Assert
    expect(container.firstChild).toBeNull();
  });

  it("cancelRequested が true のとき cancel 要求メッセージを表示する", () => {
    // Arrange
    const props = { cancelRequested: true, terminal: false };

    // Act
    render(<ExecutionStatusBanner {...props} />);

    // Assert
    expect(
      screen.getByText(new RegExp(`${uiText.actions.cancel}要求済みのため、${uiText.actions.resume}など進行系操作はできません`))
    ).toBeInTheDocument();
  });

  it("terminal が true で cancelRequested が false のとき終了メッセージを表示する", () => {
    // Arrange
    const props = { cancelRequested: false, terminal: true };

    // Act
    render(<ExecutionStatusBanner {...props} />);

    // Assert
    expect(screen.getByText(new RegExp(`${uiText.entities.execution}は終了しています`))).toBeInTheDocument();
  });

  it("両方 true のとき cancel 要求を優先して表示する", () => {
    // Arrange
    const props = { cancelRequested: true, terminal: true };

    // Act
    render(<ExecutionStatusBanner {...props} />);

    // Assert
    expect(screen.getByText(new RegExp(`${uiText.actions.cancel}要求済みのため`))).toBeInTheDocument();
  });
});
