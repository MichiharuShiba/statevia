"use client";

import { useState } from "react";
import { apiPost } from "../../lib/api";
import type { CommandAccepted, ExecutionNodeDTO, WorkflowView } from "../../lib/types";

const TERMINAL_STATUSES = new Set<string>(["Completed", "Cancelled", "Failed"]);

function isTerminalExecution(status: WorkflowView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

export function getResumeDisabledReason(
  execution: WorkflowView | null,
  node: ExecutionNodeDTO | null
): string | null {
  if (!execution) return "Execution が未読込です";
  if (!node) return "Node を選択してください";
  if (isTerminalExecution(execution.status)) return "Executionは終了しています";
  if (execution.cancelRequested)
    return "Cancel要求済みのため、Resumeなど進行系操作はできません";
  if (node.status !== "WAITING") return "WAITING 状態のノードのみ Resume できます";
  return null;
}

export type UseNodeCommandsOptions = {
  onSuccess?: () => void;
  onError?: (error: unknown) => void;
};

export function useNodeCommands(
  execution: WorkflowView | null,
  options: UseNodeCommandsOptions = {}
) {
  const { onSuccess, onError } = options;
  const [loading, setLoading] = useState(false);

  async function resumeNode(nodeId: string) {
    if (!execution) return;
    const node = execution.nodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    setLoading(true);
    try {
      await apiPost<CommandAccepted>(
        `/executions/${execution.displayId}/nodes/${node.nodeId}/resume`,
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
