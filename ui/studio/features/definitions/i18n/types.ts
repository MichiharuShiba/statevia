/**
 * definitions feature の i18n 切片型。
 * shared の UiText が合成時に取り込む。
 */
export type DefinitionsFeatureUiText = {
  definitionsPage: {
    description: string;
    pagination: {
      ariaLabel: string;
      currentPage: (page: number) => string;
    };
    search: {
      label: string;
      placeholder: string;
      submit: string;
      clear: string;
      invalidName: string;
    };
    sortByLabel: string;
    sortOrderLabel: string;
    sortByCreatedAt: string;
    sortByName: string;
    sortOrderDesc: string;
    sortOrderAsc: string;
    loading: string;
    emptyNoMatch: string;
    searchSummaryPrefix: (keyword: string) => string;
    listSummary: (totalCount: number, page: number) => string;
    createdAt: (formattedDateTime: string) => string;
    displayIdAndCreatedAt: (displayIdLabel: string, displayId: string, createdAtLabel: string) => string;
    actions: {
      openDetail: string;
      createNew: string;
      delete: string;
      restore: string;
      confirmDelete: string;
      confirmRestore: string;
      cancelConfirm: string;
      deleting: string;
      restoring: string;
    };
    includeDeleted: {
      label: string;
    };
    deletedBadge: string;
    deletedAt: (formattedDateTime: string) => string;
    toasts: {
      deleted: string;
      restored: string;
    };
    error: string;
  };
  definitionDetail: {
    title: string;
    urlPrefix: string;
    errorFetchFailed: string;
    ariaMeta: string;
    meta: {
      name: string;
      createdAt: string;
    };
    relatedExecutions: {
      title: string;
      description: string;
      openList: string;
    };
    actions: {
      title: string;
      edit: string;
      run: string;
      delete: string;
      confirmDelete: string;
      cancelConfirm: string;
      deleting: string;
    };
    toasts: {
      deleted: string;
    };
    nav: {
      backToDefinitions: string;
    };
  };
  definitionRunPage: {
    title: string;
    unspecifiedDefinitionId: string;
    definitionIdLine: (definitionIdLabel: string, definitionId: string) => string;
    inputLabelWithHint: (inputLabel: string) => string;
    inputJsonPlaceholder: string;
    toasts: {
      definitionIdRequired: (definitionIdLabel: string) => string;
      invalidInputJson: (inputLabel: string) => string;
      inputTooLarge: string;
      executionStarted: (executionDisplayId: string) => string;
    };
    nav: {
      backToDefinitionDetail: string;
    };
    actions: {
      starting: string;
      startExecution: string;
    };
    help: {
      redirectAfterStart: (runPath: string) => string;
    };
  };
};
