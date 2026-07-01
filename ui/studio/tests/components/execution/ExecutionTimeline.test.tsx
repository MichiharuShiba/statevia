import { describe, expect, it, vi } from "vitest";
import { act, fireEvent, screen } from "@testing-library/react";
import { DEFAULT_LOADING_INDICATOR_DELAY_MS } from "../../../app/lib/useDelayedVisibility";
import { ExecutionTimeline } from "../../../app/components/execution/ExecutionTimeline";
import { renderWithUiText } from "../../testUtils";
import type { ExecutionEventWithSeq } from "../../../app/lib/types";

const baseEvents: ExecutionEventWithSeq[] = [
  {
    seq: 1,
    type: "GraphUpdated",
    executionId: "ex-1",
    patch: { nodes: [{ executionNodeId: "n-1", status: "RUNNING" }] },
    at: "2026-01-15T10:00:00.000Z"
  },
  {
    seq: 2,
    type: "NodeFailed",
    executionId: "ex-1",
    nodeId: "n-1",
    at: "2026-01-15T10:01:00.000Z"
  }
];

describe("ExecutionTimeline", () => {
  it("折りたたみとイベント選択・loadMore を操作できる", () => {
    const onSelectSeq = vi.fn();
    const onLoadMore = vi.fn();

    renderWithUiText(
      <ExecutionTimeline
        events={baseEvents}
        loading={false}
        error={null}
        selectedSeq={null}
        onSelectSeq={onSelectSeq}
        onBackToCurrent={vi.fn()}
        isReplaying={false}
        hasMore
        loadingMore={false}
        onLoadMore={onLoadMore}
      />
    );

    fireEvent.click(screen.getByRole("button", { name: /イベントタイムライン/i }));
    expect(screen.getByText(/#1/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /#1/ }));
    expect(onSelectSeq).toHaveBeenCalledWith(1);

    fireEvent.click(screen.getByRole("button", { name: /続きを読み込む/i }));
    expect(onLoadMore).toHaveBeenCalled();
  });

  it("error と loading / empty を表示する", () => {
    vi.useFakeTimers();
    const { rerender } = renderWithUiText(
      <ExecutionTimeline
        events={[]}
        loading
        error={null}
        selectedSeq={null}
        onSelectSeq={vi.fn()}
        onBackToCurrent={vi.fn()}
        isReplaying={false}
      />
    );

    act(() => {
      vi.advanceTimersByTime(DEFAULT_LOADING_INDICATOR_DELAY_MS);
    });
    expect(screen.getByText(/ローディング/)).toBeInTheDocument();
    vi.useRealTimers();

    rerender(
      <ExecutionTimeline
        events={[]}
        loading={false}
        error={new Error("boom")}
        selectedSeq={null}
        onSelectSeq={vi.fn()}
        onBackToCurrent={vi.fn()}
        isReplaying={false}
      />
    );
    fireEvent.click(screen.getByRole("button", { name: /イベントタイムライン/i }));
    expect(screen.getByText("boom")).toBeInTheDocument();

    rerender(
      <ExecutionTimeline
        events={[]}
        loading={false}
        error={null}
        selectedSeq={null}
        onSelectSeq={vi.fn()}
        onBackToCurrent={vi.fn()}
        isReplaying={false}
      />
    );
    expect(screen.getByText(/イベントがありません/)).toBeInTheDocument();
  });
});
