import type { ExecutionStatus } from "@/shared/lib/statusStyle";

/**
 * ダッシュボードが一覧表示する実行（GET /executions の要素のうち利用分）。
 * 正本の実行 DTO は `features/executions/types`。feature 間 import を避けるため表示用に切り出す。
 */
export type DashboardExecutionItem = {
  displayId: string;
  status: ExecutionStatus;
  startedAt: string;
  updatedAt?: string | null;
};

/** ダッシュボード用のページング付き実行一覧。 */
export type DashboardExecutionsPage = {
  items: DashboardExecutionItem[];
  totalCount: number;
};
