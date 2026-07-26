import type { DefinitionsFeatureUiText } from "./types";
import { definitionsUiTextJa } from "./ja";

/**
 * definitions feature の英語辞書切片。
 * 未翻訳キーは日本語切片を継承する。
 */
export const definitionsUiTextEn: DefinitionsFeatureUiText = {
  definitionsPage: {
    ...definitionsUiTextJa.definitionsPage,
    description: "Search and paginate definitions, then open the detail page.",
    loading: "Loading definitions.",
    emptyNoMatch: "No definitions matched. Change the query or clear filters.",
    searchSummaryPrefix: (keyword: string) => `Search: "${keyword}" / `,
    listSummary: (totalCount: number, page: number) => `Total ${totalCount} (page ${page})`,
    createdAt: (formattedDateTime: string) => `Created: ${formattedDateTime}`,
    error: "Failed to load definitions.",
    pagination: {
      ...definitionsUiTextJa.definitionsPage.pagination,
      ariaLabel: "Definitions pagination",
      currentPage: (page: number) => `Page ${page}`,
    },
    search: {
      label: "Name search (partial match)",
      placeholder: "e.g. order",
      submit: "Search",
      clear: "Clear",
      invalidName: "Search keyword allows only ASCII alphanumerics plus . - _ within 100 characters.",
    },
    sortByLabel: "Sort by",
    sortOrderLabel: "Order",
    sortByCreatedAt: "Created at",
    sortByName: "Name",
    sortOrderDesc: "Descending",
    sortOrderAsc: "Ascending",
    actions: {
      openDetail: "Open details",
      createNew: "Create new definition",
      delete: "Delete",
      restore: "Restore",
      confirmDelete: "Confirm delete",
      confirmRestore: "Confirm restore",
      cancelConfirm: "Cancel",
      deleting: "Deleting...",
      restoring: "Restoring...",
    },
    includeDeleted: {
      label: "Include deleted",
    },
    deletedBadge: "Deleted",
    deletedAt: (formattedDateTime: string) => `Deleted: ${formattedDateTime}`,
    toasts: {
      deleted: "Definition deleted.",
      restored: "Definition restored.",
    },
  },
  definitionDetail: {
    ...definitionsUiTextJa.definitionDetail,
    title: "Definition detail",
    urlPrefix: "URL:",
    errorFetchFailed: "Failed to fetch definition.",
    ariaMeta: "Definition metadata",
    meta: {
      name: "Name",
      createdAt: "Created at",
    },
    relatedExecutions: {
      title: "Related executions",
      description: "Go to the list of executions related to this definition.",
      openList: "Open executions list",
    },
    actions: {
      title: "Edit / Run",
      edit: "Edit definition",
      run: "Start new execution",
      delete: "Delete definition",
      confirmDelete: "Confirm delete",
      cancelConfirm: "Cancel",
      deleting: "Deleting...",
    },
    toasts: {
      deleted: "Definition deleted.",
    },
    nav: {
      backToDefinitions: "Back to definitions",
    },
  },
  definitionRunPage: {
    ...definitionsUiTextJa.definitionRunPage,
    title: "Run definition",
    unspecifiedDefinitionId: "(not specified)",
    inputLabelWithHint: (inputLabel: string) => `${inputLabel} (optional, JSON)`,
    inputJsonPlaceholder: 'Example: {"orderId":"123"}',
    nav: {
      backToDefinitionDetail: "Back to definition detail",
    },
    actions: {
      starting: "Starting...",
      startExecution: "Start execution",
    },
    toasts: {
      definitionIdRequired: (definitionIdLabel: string) => `${definitionIdLabel} is required.`,
      invalidInputJson: (inputLabel: string) => `${inputLabel} JSON is invalid.`,
      inputTooLarge: "Input must be within 65536 bytes (64KiB).",
      executionStarted: (executionDisplayId: string) => `Execution started: ${executionDisplayId}`,
    },
    help: {
      redirectAfterStart: (runPath: string) => `After starting, you will be redirected to ${runPath}.`,
    },
  },
};
