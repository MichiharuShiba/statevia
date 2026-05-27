import { vi } from "vitest";
import type { ExecutionView } from "../../app/lib/types";

/**
 * `useExecution` モックの戻り値を組み立てる。
 */
export function buildUseExecutionMock(execution: ExecutionView | null = null) {
  return {
    execution,
    loading: false,
    canCancel: false,
    terminal: false,
    loadExecution: vi.fn(),
    cancelExecution: vi.fn(),
    publishEvent: vi.fn(),
    selectedNodeId: null,
    setSelectedNodeId: vi.fn()
  };
}
