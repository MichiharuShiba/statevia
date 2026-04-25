"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../../components/execution/ExecutionDashboard";
import { ActionLinkGroup } from "../../../components/layout/ActionLinkGroup";
import { PageShell } from "../../../components/layout/PageShell";
import { PageState } from "../../../components/layout/PageState";

/**
 * Graph 専用ページ。可視化体験を中心に表示し、詳細/実行画面と往復できる。
 */
export default function WorkflowGraphPage() {
  const params = useParams();
  const workflowId = useMemo(() => {
    const raw = params.workflowId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.workflowId]);

  if (!workflowId.trim()) {
    return (
      <PageShell
        title="ワークフローグラフ"
        primaryActions={<ActionLinkGroup links={[{ label: "Workflow 一覧", href: "/workflows", priority: "primary" }]} />}
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
      headerTitle="ワークフローグラフ"
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={false}
      initialViewMode="graph"
      lockViewMode={true}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: "詳細", href: `/workflows/${encodeURIComponent(workflowId)}`, priority: "primary" },
            { label: "実行", href: `/workflows/${encodeURIComponent(workflowId)}/run` },
            { label: "Workflow 一覧", href: "/workflows" },
            { label: "ダッシュボード", href: "/dashboard" }
          ]}
        />
      }
    />
  );
}
