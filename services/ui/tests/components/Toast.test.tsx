import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { Toast } from "../../app/components/Toast";

describe("Toast", () => {
  it("toast が null のとき null を返す", () => {
    // Arrange
    const props = { toast: null, onClose: () => {} };

    // Act
    const { container } = render(<Toast {...props} />);

    // Assert
    expect(container.firstChild).toBeNull();
  });

  it("toast があるときメッセージを表示する", () => {
    // Arrange
    const props = {
      toast: { tone: "success" as const, message: "Saved successfully" },
      onClose: () => {}
    };

    // Act
    render(<Toast {...props} />);

    // Assert
    expect(screen.getByText("Saved successfully")).toBeInTheDocument();
  });

  it("error トーンのスタイルを表示する", () => {
    // Arrange
    const props = {
      toast: { tone: "error" as const, message: "Failed" },
      onClose: () => {}
    };

    // Act
    render(<Toast {...props} />);

    // Assert
    expect(screen.getByText("Failed")).toBeInTheDocument();
  });

  it("閉じるボタンクリックで onClose を呼ぶ", () => {
    // Arrange
    const onClose = vi.fn();
    render(
      <Toast
        toast={{ tone: "info", message: "Info" }}
        onClose={onClose}
      />
    );
    const button = screen.getByRole("button", { name: "close toast" });

    // Act
    fireEvent.click(button);

    // Assert
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("info トーンのメッセージを表示する", () => {
    // Arrange
    const props = {
      toast: { tone: "info" as const, message: "Information message" },
      onClose: () => {}
    };

    // Act
    render(<Toast {...props} />);

    // Assert
    expect(screen.getByText("Information message")).toBeInTheDocument();
  });

  it("長いメッセージも省略せず表示する", () => {
    // Arrange
    const longMessage = "A".repeat(200);
    const props = {
      toast: { tone: "success" as const, message: longMessage },
      onClose: () => {}
    };

    // Act
    render(<Toast {...props} />);

    // Assert
    expect(screen.getByText(longMessage)).toBeInTheDocument();
  });

  it("閉じるボタンがアクセシブルである", () => {
    // Arrange
    const props = {
      toast: { tone: "error" as const, message: "Error" },
      onClose: () => {}
    };

    // Act
    render(<Toast {...props} />);

    // Assert
    expect(screen.getByRole("button", { name: "close toast" })).toBeInTheDocument();
  });
});
