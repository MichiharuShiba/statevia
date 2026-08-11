import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { WaitEventsEditor } from "@/features/definition-editor/ui/WaitEventsEditor";

const labels = {
  waitEventsSectionTitle: "events",
  waitEventNameLabel: "Event name",
  waitEventTargetLabel: "Target",
  waitEventsAdd: "Add",
  waitEventsRemove: "Remove"
};

describe("WaitEventsEditor", () => {
  it("events の一部更新でも既存行の input DOM を維持する（key 安定）", () => {
    // Arrange
    const onEventsChange = vi.fn();
    const { rerender } = render(
      <WaitEventsEditor
        events={{ approve: "ok", reject: "ng" }}
        labels={labels}
        onEventsChange={onEventsChange}
      />
    );
    const approveNameInput = screen.getByDisplayValue("approve");

    // Act — 別イベントの遷移先だけ変わる（行 id が再生成されると DOM が差し替わる）
    rerender(
      <WaitEventsEditor
        events={{ approve: "ok", reject: "done" }}
        labels={labels}
        onEventsChange={onEventsChange}
      />
    );

    // Assert
    expect(screen.getByDisplayValue("approve")).toBe(approveNameInput);
    expect(screen.getByDisplayValue("done")).toBeInTheDocument();
  });

  it("行追加で onEventsChange を呼ぶ", () => {
    // Arrange
    const onEventsChange = vi.fn();
    render(
      <WaitEventsEditor
        events={{ resume: "a" }}
        labels={labels}
        onEventsChange={onEventsChange}
      />
    );

    // Act
    fireEvent.click(screen.getByRole("button", { name: labels.waitEventsAdd }));

    // Assert
    expect(onEventsChange).toHaveBeenCalledWith({ resume: "a", event1: "" });
  });
});
