"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../../components/execution/ExecutionDashboard";
import { ActionLinkGroup } from "../../../components/layout/ActionLinkGroup";
import { PageShell } from "../../../components/layout/PageShell";
import { PageState } from "../../../components/layout/PageState";
import { useUiText } from "../../../lib/uiTextContext";

/**
 * Run 専用ページ。実行操作（Cancel / Resume / Event 送信）をここに集約する。
 */
export default function WorkflowRunPage() {
  const uiText = useUiText();
  const params = useParams();
  const workflowId = useMemo(() => {
    const raw = params.workflowId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.workflowId]);

  if (!workflowId.trim()) {
    return (
      <PageShell
        title={uiText.workflowRunPage.title}
        primaryActions={<ActionLinkGroup links={[{ label: uiText.lists.workflows, href: "/workflows", priority: "primary" }]} />}
      >
        <PageState state="error" message={uiText.workflowRunPage.missingWorkflowId} />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={workflowId}
      initialExecutionId={workflowId}
      autoLoadOnMount
      headerTitle={uiText.workflowRunPage.title}
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={true}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: uiText.workflowRunPage.navDetail, href: `/workflows/${encodeURIComponent(workflowId)}`, priority: "primary" }
          ]}
        />
      }
    />
  );
}
