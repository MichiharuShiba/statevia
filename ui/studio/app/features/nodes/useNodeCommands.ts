"use client";

import { useState } from "react";
import { apiPost } from "../../lib/api";
import { DEFAULT_LOCALE, type Locale } from "../../lib/i18n";
import type { CommandAccepted, ExecutionNodeDTO, ExecutionView } from "../../lib/types";
import { getUiText } from "../../lib/uiTextLocale";

const TERMINAL_STATUSES = new Set<string>(["Completed", "Cancelled", "Failed"]);

function isTerminalExecution(status: ExecutionView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

/** getResumeDisabledReason。 */
export function getResumeDisabledReason(
  execution: ExecutionView | null,
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

/** useNodeCommands のオプション。 */
export type UseNodeCommandsOptions = {
  onSuccess?: () => void;
  onError?: (error: unknown) => void;
  commandsEnabled?: boolean;
};

/** ノードへの Resume 等コマンドを発行するフック。 */
export function useNodeCommands(
  execution: ExecutionView | null,
  options: UseNodeCommandsOptions = {}
) {
  const { onSuccess, onError, commandsEnabled = true } = options;
  const [loading, setLoading] = useState(false);

  async function resumeNode(nodeId: string) {
    if (!commandsEnabled) return;
    if (!execution) return;
    const node = execution.nodes.find((n) => n.executionNodeId === nodeId);
    if (!node) return;
    setLoading(true);
    try {
      await apiPost<CommandAccepted>(
        `/executions/${execution.displayId}/nodes/${node.executionNodeId}/resume`,
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
