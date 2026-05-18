import { describe, expect, it, vi } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import { ListPagination } from "../../../app/components/layout/ListPagination";
import { renderWithUiText } from "../../testUtils";

describe("ListPagination", () => {
  it("前後ページ操作を発火する", () => {
    const onPrev = vi.fn();
    const onNext = vi.fn();

    renderWithUiText(
      <ListPagination
        currentPageLabel="1 / 3"
        hasPrev
        hasNext
        onPrev={onPrev}
        onNext={onNext}
        ariaLabel="definitions pagination"
      />
    );

    fireEvent.click(screen.getAllByRole("button")[0]);
    fireEvent.click(screen.getAllByRole("button")[1]);
    expect(onPrev).toHaveBeenCalledTimes(1);
    expect(onNext).toHaveBeenCalledTimes(1);
  });
});
