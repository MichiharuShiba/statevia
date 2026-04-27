"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../components/execution/ExecutionDashboard";
import { ActionLinkGroup } from "../../components/layout/ActionLinkGroup";
import { PageShell } from "../../components/layout/PageShell";
import { PageState } from "../../components/layout/PageState";
import { uiText } from "../../lib/uiText";

/**
 * ワークフロー単体（URL 表示 ID 確定）の参照画面。
 */
export default function WorkflowDetailPage() {
  const params = useParams();
  const workflowId = useMemo(() => {
    const raw = params.workflowId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.workflowId]);

  if (!workflowId.trim()) {
    return (
      <PageShell
        title={uiText.workflowDetailPage.title}
        primaryActions={<ActionLinkGroup links={[{ label: uiText.lists.workflows, href: "/workflows", priority: "primary" }]} />}
      >
        <PageState state="error" message={uiText.workflowDetailPage.missingWorkflowId} />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={workflowId}
      initialExecutionId={workflowId}
      autoLoadOnMount
      headerTitle={uiText.workflowDetailPage.title}
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={false}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: uiText.workflowDetailPage.navRun, href: `/workflows/${encodeURIComponent(workflowId)}/run`, priority: "primary" },
            { label: uiText.workflowDetailPage.navGraph, href: `/workflows/${encodeURIComponent(workflowId)}/graph` },
            { label: uiText.lists.workflows, href: "/workflows" },
            { label: uiText.navigation.dashboard, href: "/dashboard" }
          ]}
        />
      }
    />
  );
}
