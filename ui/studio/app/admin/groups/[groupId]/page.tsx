import { AdminGroupDetailPageClient } from "./AdminGroupDetailPageClient";

type PageProps = Readonly<{
  params: Promise<{ groupId: string }>;
}>;

/**
 * グループ詳細（メンバー・権限編集）。
 */
export default async function AdminGroupDetailPage({ params }: PageProps) {
  const { groupId } = await params;
  return <AdminGroupDetailPageClient groupId={groupId} />;
}
