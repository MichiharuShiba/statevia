"use client";

import { useState } from "react";
import { apiPost } from "@/shared/api";
import { DEFAULT_LOCALE, type Locale } from "@/shared/i18n/i18n";
import type { CommandAccepted, ExecutionNodeDTO, ExecutionView } from "../types";
import { getUiText } from "@/shared/i18n/uiTextLocale";
import { resolveWaitResumeEvents } from "../lib/waitResumeEvents";

const TERMINAL_STATUSES = new Set<string>(["Completed", "Cancelled", "Failed"]);

function isTerminalExecution(status: ExecutionView["status"]): boolean {
  return TERMINAL_STATUSES.has(status);
}

/**
 * Resume ボタンを無効化すべき理由を返す（操作可能なら null）。
 *
 * @param execution 実行ビュー（未読込なら null）。
 * @param node 対象ノード（未選択なら null）。
 * @param commandsEnabled Run 画面などで操作が有効か。
 * @param locale UI 文言ロケール。
 * @returns 無効化理由。操作可能なら null。
 */
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
  if (resolveWaitResumeEvents(node).length === 0)
    return uiText.nodeCommands.resumeDisabledReason.noResumeEvent;
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

  /**
   * Wait ノードを指定イベント名で Resume する。
   *
   * @param nodeId 実行ノード短名 UUID（`nodeId`）。
   * @param resumeKey 再開イベント名（`resumeKey` として API に送る）。
   */
  async function resumeNode(nodeId: string, resumeKey: string) {
    if (!commandsEnabled) return;
    if (!execution) return;
    const eventName = resumeKey.trim();
    if (eventName.length === 0) return;
    const node = execution.nodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    setLoading(true);
    try {
      await apiPost<CommandAccepted>(
        `/executions/${execution.displayId}/nodes/${node.nodeId}/resume`,
        { resumeKey: eventName }
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
