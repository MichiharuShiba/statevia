import { definitionsUiTextEn } from "@/features/definitions/i18n";
import { authUiTextEn } from "@/features/auth/i18n";
import { adminUiTextEn } from "@/features/admin/i18n";
import { dashboardUiTextEn } from "@/features/dashboard/i18n";
import { definitionEditorUiTextEn } from "@/features/definition-editor/i18n";
import { executionsUiTextEn } from "@/features/executions/i18n";
import type { UiText } from "./uiText";
import { actionCatalogUiTextEnOverrides, actionCatalogUiTextJa } from "./actionCatalogUiText";
import { uiTextJa } from "./uiText";

/**
 * 英語辞書。段階移行のため、未翻訳キーは日本語辞書を継承する。
 */
export const uiTextEn: UiText = {
  ...uiTextJa,
  ...definitionsUiTextEn,
  ...authUiTextEn,
  ...adminUiTextEn,
  ...dashboardUiTextEn,
  ...definitionEditorUiTextEn,
  ...executionsUiTextEn,
  actionCatalog: {
    ...actionCatalogUiTextJa,
    ...actionCatalogUiTextEnOverrides,
  },
  actionLinks: {
    aria: {
      navigation: "Navigation links",
    },

  },
  navigation: {
    dashboard: "Dashboard",
    definitions: "Definitions",
    executions: "Executions",
    health: "Health",
    adminUsers: "Users",
    adminGroups: "Groups",
    adminApiKeys: "API keys",
    account: "Account",
    logout: "Sign out",

  },
  entities: {
    definition: "Definition",
    execution: "Execution",
    node: "Node",

  },
  lists: {
    ...uiTextJa.lists,
    executions: "Executions",
    definitions: "Definitions",
    nodeCount: (count: number) => `${count} items`,

  },
  actions: {
    ...uiTextJa.actions,
    load: "Load",
    loading: "Loading...",
    reload: "Reload",
    cancel: "Cancel",
    resume: "Resume",
    retry: "Retry",
    save: "Save",
    sendEvent: "Send event",
    openDetail: "Details",
    closeToast: "Close notification",
    viewList: "List",
    viewGraph: "Graph",

  },
  pagination: {
    prev: "Prev",
    next: "Next",

  },
  labels: {
    ...uiTextJa.labels,
    status: "Status",
    nodeId: "Node ID",
    definitionId: "Definition ID",
    displayId: "Display ID",
    resourceId: "Resource ID",
    graphId: "Graph ID",
    input: "Input",
    definitionEditor: "Definition Editor",

  },
  pageState: {
    loading: "Loading...",
    empty: "No data to display.",
    error: "Failed to load data.",

  },
  errorPrefixes: {
    unauthorized401: "401 Authentication required",
    forbidden403: "403 Insufficient permission or tenant is missing",
    conflict409: "409 State conflict",
    unprocessable422: "422 Invalid input",
    server500: "500 Server error",

  },
  tenantMissingBanner: {
    noticeParts: (loadLabel: string, cancelLabel: string, resumeLabel: string) => ({
      beforePrimaryEnv: `Tenant is not set. ${loadLabel} / ${cancelLabel} / ${resumeLabel} may fail. Set `,
      betweenEnvs: " or configure ",
      afterSecondaryEnv: " in the server environment.",
    }),

  },
};
