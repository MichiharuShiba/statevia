import "@testing-library/jest-dom/vitest";

/** jsdom: React Flow / CodeMirror が参照する ResizeObserver の最小スタブ */
class ResizeObserverStub {
  observe(): void {
    // jsdom には ResizeObserver が無いため no-op（API の形だけ提供）
  }

  unobserve(): void {
    // jsdom には ResizeObserver が無いため no-op（API の形だけ提供）
  }

  disconnect(): void {
    // jsdom には ResizeObserver が無いため no-op（API の形だけ提供）
  }
}

globalThis.ResizeObserver ??= ResizeObserverStub;

/** jsdom: React Flow が参照する DOMMatrixReadOnly の最小スタブ（`new` 可能な関数） */
function DOMMatrixReadOnlyStub(_init?: string | number[]) {
  // jsdom には DOMMatrixReadOnly が無いため no-op（API の形だけ提供）
}

globalThis.DOMMatrixReadOnly ??= DOMMatrixReadOnlyStub as unknown as typeof DOMMatrixReadOnly;
