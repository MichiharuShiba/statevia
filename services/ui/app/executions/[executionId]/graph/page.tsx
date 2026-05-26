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
export default function ExecutionGraphPage() {
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
        title={uiText.executionGraphPage.title}
        primaryActions={<ActionLinkGroup links={[{ label: uiText.lists.executions, href: "/executions", priority: "primary" }]} />}
      >
        <PageState state="error" message={uiText.executionGraphPage.missingExecutionId} />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={executionId}
      initialExecutionId={executionId}
      autoLoadOnMount
      headerTitle={uiText.executionGraphPage.title}
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={false}
      initialViewMode="graph"
      lockViewMode={true}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: uiText.executionGraphPage.navDetail, href: `/executions/${encodeURIComponent(executionId)}`, priority: "primary" },
            { label: uiText.executionGraphPage.navRun, href: `/executions/${encodeURIComponent(executionId)}/run` }
          ]}
        />
      }
    />
  );
}
