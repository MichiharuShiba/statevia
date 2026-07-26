/**
 * dashboard feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type DashboardFeatureUiText = {
  dashboard: {
    title: string;
    descriptionRecent: string;
    loadingRecent: string;
    emptyStartFromDefinitionsOrExecutions: string;
    totalCount: (count: number | null) => string;
    updatedAt: (formattedDateTime: string) => string;
    aria: {
      recentExecutionsList: string;
    };
    actions: {
      openDetail: string;
    };
    error: {
      fetchFailed: string;
    };

  };
};
