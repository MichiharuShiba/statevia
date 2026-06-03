import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useDelayedVisibility } from "../../app/lib/useDelayedVisibility";

describe("useDelayedVisibility", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("active 直後は false、遅延後に true になる", () => {
    const { result, rerender } = renderHook(
      ({ active, delayMs }) => useDelayedVisibility(active, delayMs),
      { initialProps: { active: false, delayMs: 300 } }
    );

    rerender({ active: true, delayMs: 300 });
    expect(result.current).toBe(false);

    act(() => {
      vi.advanceTimersByTime(300);
    });
    expect(result.current).toBe(true);
  });

  it("active が false になると遅延なく false になる", () => {
    const { result, rerender } = renderHook(
      ({ active }) => useDelayedVisibility(active, 300),
      { initialProps: { active: true } }
    );

    act(() => {
      vi.advanceTimersByTime(300);
    });
    expect(result.current).toBe(true);

    rerender({ active: false });
    expect(result.current).toBe(false);
  });

  it("delayMs が 0 以下のときは遅延なく true になる", () => {
    const { result, rerender } = renderHook(
      ({ active, delayMs }) => useDelayedVisibility(active, delayMs),
      { initialProps: { active: false, delayMs: 0 } }
    );

    rerender({ active: true, delayMs: 0 });
    expect(result.current).toBe(true);
  });

  it("遅延前に active が終了したら表示しない", () => {
    const { result, rerender } = renderHook(
      ({ active }) => useDelayedVisibility(active, 300),
      { initialProps: { active: true } }
    );

    act(() => {
      vi.advanceTimersByTime(100);
    });
    rerender({ active: false });

    act(() => {
      vi.advanceTimersByTime(300);
    });
    expect(result.current).toBe(false);
  });
});
