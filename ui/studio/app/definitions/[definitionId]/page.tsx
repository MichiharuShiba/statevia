import { DefinitionDetailClient } from "./DefinitionDetailClient";

type DefinitionDetailPageProps = {
  params: Promise<{
    definitionId: string;
  }>;
};

/**
 * Definition 詳細ページ（URL の definitionId から `DefinitionDetailClient` へ引き渡す）。
 */
export default async function DefinitionDetailPage({ params }: Readonly<DefinitionDetailPageProps>) {
  const { definitionId } = await params;
  return <DefinitionDetailClient definitionId={definitionId} />;
}
