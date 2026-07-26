import { AdminGroupDetailPageClient } from "@/features/admin/ui/AdminGroupDetailPageClient";

type PageProps = Readonly<{
  params: Promise<{ groupId: string }>;
}>;

/**
 * グループ詳細（薄い route wrapper）。
 */
export default async function AdminGroupDetailPage({ params }: PageProps) {
  const { groupId } = await params;
  return <AdminGroupDetailPageClient groupId={groupId} />;
}
