"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../../components/execution/ExecutionDashboard";
import { ActionLinkGroup } from "../../../components/layout/ActionLinkGroup";
import { PageShell } from "../../../components/layout/PageShell";
import { PageState } from "../../../components/layout/PageState";
import { uiText } from "../../../lib/uiText";

/**
 * Run 専用ページ。実行操作（Cancel / Resume / Event 送信）をここに集約する。
 */
export default function WorkflowRunPage() {
  const params = useParams();
  const workflowId = useMemo(() => {
    const raw = params.workflowId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.workflowId]);

  if (!workflowId.trim()) {
    return (
      <PageShell
        title="ワークフロー実行"
        primaryActions={<ActionLinkGroup links={[{ label: uiText.lists.workflows, href: "/workflows", priority: "primary" }]} />}
      >
        <PageState state="error" message="ワークフロー ID が指定されていません。" />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={workflowId}
      initialExecutionId={workflowId}
      autoLoadOnMount
      headerTitle="ワークフロー実行"
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={true}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: "詳細", href: `/workflows/${encodeURIComponent(workflowId)}`, priority: "primary" },
            { label: "グラフ", href: `/workflows/${encodeURIComponent(workflowId)}/graph` },
            { label: uiText.lists.workflows, href: "/workflows" },
            { label: uiText.navigation.dashboard, href: "/dashboard" }
          ]}
        />
      }
    />
  );
}
