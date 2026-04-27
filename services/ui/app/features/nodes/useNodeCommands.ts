"use client";

import { useState } from "react";
import { apiPost } from "../../lib/api";
import { DEFAULT_LOCALE, type Locale } from "../../lib/i18n";
import type { CommandAccepted, ExecutionNodeDTO, WorkflowView } from "../../lib/types";
import { getUiText } from "../../lib/uiTextLocale";

const TERMINAL_STATUSES = new Set<string>(["Completed", "Cancelled", "Failed"]);

function isTerminalExecution(status: WorkflowView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

export function getResumeDisabledReason(
  execution: WorkflowView | null,
  node: ExecutionNodeDTO | null,
  commandsEnabled = true,
  locale: Locale = DEFAULT_LOCALE
): string | null {
  const uiText = getUiText(locale);
  if (!commandsEnabled) return uiText.nodeCommands.resumeDisabledReason.runOnly;
  if (!execution) return uiText.nodeCommands.resumeDisabledReason.executionNotLoaded;
  if (!node) return uiText.nodeCommands.resumeDisabledReason.nodeNotSelected;
  if (isTerminalExecution(execution.status)) return uiText.nodeCommands.resumeDisabledReason.executionTerminal;
  if (execution.cancelRequested)
    return uiText.nodeCommands.resumeDisabledReason.cancelRequested;
  if (node.status !== "WAITING") return uiText.nodeCommands.resumeDisabledReason.waitingOnly;
  return null;
}

export type UseNodeCommandsOptions = {
  onSuccess?: () => void;
  onError?: (error: unknown) => void;
  commandsEnabled?: boolean;
};

export function useNodeCommands(
  execution: WorkflowView | null,
  options: UseNodeCommandsOptions = {}
) {
  const { onSuccess, onError, commandsEnabled = true } = options;
  const [loading, setLoading] = useState(false);

  async function resumeNode(nodeId: string) {
    if (!commandsEnabled) return;
    if (!execution) return;
    const node = execution.nodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    setLoading(true);
    try {
      await apiPost<CommandAccepted>(
        `/workflows/${execution.displayId}/nodes/${node.nodeId}/resume`,
        { resumeKey: node.waitKey ?? undefined }
      );
      onSuccess?.();
    } catch (error) {
      onError?.(error);
    } finally {
      setLoading(false);
    }
  }

  return { resumeNode, loading };
}
