import type { DashboardFeatureUiText } from "./types";
import { dashboardUiTextJa } from "./ja";

/**
 * dashboard feature の英語辞書切片（未翻訳は ja を継承）。
 */
export const dashboardUiTextEn: DashboardFeatureUiText = {
  ...dashboardUiTextJa,
  dashboard: {
    ...dashboardUiTextJa.dashboard,
    title: "Dashboard",
    descriptionRecent: "Recent executions (up to 10).",
    loadingRecent: "Loading recent executions.",
    emptyStartFromDefinitionsOrExecutions: "Start from Definitions or Executions.",
    totalCount: (count: number | null) => (count == null ? "Total: --" : `Total: ${count}`),
    updatedAt: (formattedDateTime: string) => `Updated: ${formattedDateTime}`,
    aria: {
      recentExecutionsList: "Recent executions",
    },
    actions: {
      openDetail: "Open details",
    },
    error: {
      fetchFailed: "Failed to fetch data.",
    },

  },
};
