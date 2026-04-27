"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../../components/execution/ExecutionDashboard";
import { ActionLinkGroup } from "../../../components/layout/ActionLinkGroup";
import { PageShell } from "../../../components/layout/PageShell";
import { PageState } from "../../../components/layout/PageState";
import { useUiText } from "../../../lib/uiTextContext";

/**
 * Graph 専用ページ。可視化体験を中心に表示し、詳細/実行画面と往復できる。
 */
export default function WorkflowGraphPage() {
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
        title={uiText.workflowGraphPage.title}
        primaryActions={<ActionLinkGroup links={[{ label: uiText.lists.workflows, href: "/workflows", priority: "primary" }]} />}
      >
        <PageState state="error" message={uiText.workflowGraphPage.missingWorkflowId} />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={workflowId}
      initialExecutionId={workflowId}
      autoLoadOnMount
      headerTitle={uiText.workflowGraphPage.title}
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={false}
      initialViewMode="graph"
      lockViewMode={true}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: uiText.workflowGraphPage.navDetail, href: `/workflows/${encodeURIComponent(workflowId)}`, priority: "primary" },
            { label: uiText.workflowGraphPage.navRun, href: `/workflows/${encodeURIComponent(workflowId)}/run` },
            { label: uiText.lists.workflows, href: "/workflows" },
            { label: uiText.navigation.dashboard, href: "/dashboard" }
          ]}
        />
      }
    />
  );
}
