"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../components/execution/ExecutionDashboard";
import { ActionLinkGroup } from "../../components/layout/ActionLinkGroup";
import { PageShell } from "../../components/layout/PageShell";
import { PageState } from "../../components/layout/PageState";
import { useUiText } from "../../lib/uiTextContext";

/**
 * ワークフロー単体（URL 表示 ID 確定）の参照画面。
 */
export default function ExecutionDetailPage() {
  const uiText = useUiText();
  const params = useParams();
  const executionId = useMemo(() => {
    const raw = params.executionId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.executionId]);

  if (!executionId.trim()) {
    return (
      <PageShell
        title={uiText.executionDetailPage.title}
        primaryActions={<ActionLinkGroup links={[{ label: uiText.lists.executions, href: "/executions", priority: "primary" }]} />}
      >
        <PageState state="error" message={uiText.executionDetailPage.missingExecutionId} />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={executionId}
      initialExecutionId={executionId}
      autoLoadOnMount
      headerTitle={uiText.executionDetailPage.title}
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={false}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: uiText.executionDetailPage.navRun, href: `/executions/${encodeURIComponent(executionId)}/run`, priority: "primary" }
          ]}
        />
      }
    />
  );
}
