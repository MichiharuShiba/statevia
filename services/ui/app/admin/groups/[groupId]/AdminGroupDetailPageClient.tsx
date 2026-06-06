"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useState } from "react";
import { PageShell } from "../../../components/layout/PageShell";
import { PageState } from "../../../components/layout/PageState";
import { Toast } from "../../../components/Toast";
import { apiGet, apiPut } from "../../../lib/api";
import type { AdminGroupDetail, AdminUserListItem, PermissionDefinitionDto } from "../../../lib/adminTypes";
import { toToastError, type ToastState } from "../../../lib/errors";
import { useUiText } from "../../../lib/uiTextContext";

type AdminGroupDetailPageClientProps = Readonly<{
  groupId: string;
}>;

/**
 * チェックボックス用の HTML id（semantic key の `.` 等を置換）。
 */
function checkboxInputId(prefix: string, key: string): string {
  return `${prefix}-${key.replaceAll(/[^a-zA-Z0-9_-]/g, "-")}`;
}

/**
 * ユーザー行の表示ラベル（無効ユーザーは inactive 表記を付与）。
 */
function memberCheckboxLabel(user: AdminUserListItem, inactiveLabel: string): string {
  return user.isActive ? user.email : `${user.email} (${inactiveLabel})`;
}

/**
 * グループのメンバー・権限を編集する。
 */
export function AdminGroupDetailPageClient({ groupId }: AdminGroupDetailPageClientProps) {
  const uiText = useUiText();
  const [group, setGroup] = useState<AdminGroupDetail | null>(null);
  const [users, setUsers] = useState<AdminUserListItem[]>([]);
  const [permissions, setPermissions] = useState<PermissionDefinitionDto[]>([]);
  const [selectedUserIds, setSelectedUserIds] = useState<Set<string>>(new Set());
  const [selectedPermissionKeys, setSelectedPermissionKeys] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [savingMembers, setSavingMembers] = useState(false);
  const [savingPermissions, setSavingPermissions] = useState(false);
  const [toast, setToast] = useState<ToastState | null>(null);

  const assignablePermissions = useMemo(
    () => permissions.filter((p) => p.permissionKey !== "tenant.admin" && !p.isDeprecated),
    [permissions]
  );

  const load = useCallback(async () => {
    setLoading(true);
    setToast(null);
    try {
      const [groupDetail, userList, permissionList] = await Promise.all([
        apiGet<AdminGroupDetail>(`/admin/groups/${groupId}`),
        apiGet<AdminUserListItem[]>("/admin/users"),
        apiGet<PermissionDefinitionDto[]>("/admin/permissions")
      ]);
      setGroup(groupDetail);
      setUsers(userList);
      setPermissions(permissionList);
      setSelectedUserIds(new Set(groupDetail.memberUserIds));
      setSelectedPermissionKeys(new Set(groupDetail.permissionKeys));
    } catch (error) {
      setToast(toToastError(error));
      setGroup(null);
    } finally {
      setLoading(false);
    }
  }, [groupId]);

  useEffect(() => {
    void load();
  }, [load]);

  function toggleUser(userId: string) {
    setSelectedUserIds((prev) => {
      const next = new Set(prev);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
  }

  function togglePermission(key: string) {
    setSelectedPermissionKeys((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  async function saveMembers() {
    setSavingMembers(true);
    setToast(null);
    try {
      const updated = await apiPut<AdminGroupDetail>(`/admin/groups/${groupId}/members`, {
        userIds: [...selectedUserIds]
      });
      setGroup(updated);
      setSelectedUserIds(new Set(updated.memberUserIds));
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSavingMembers(false);
    }
  }

  async function savePermissions() {
    setSavingPermissions(true);
    setToast(null);
    try {
      const updated = await apiPut<AdminGroupDetail>(`/admin/groups/${groupId}/permissions`, {
        permissionKeys: [...selectedPermissionKeys]
      });
      setGroup(updated);
      setSelectedPermissionKeys(new Set(updated.permissionKeys));
    } catch (error) {
      setToast(toToastError(error));
    } finally {
      setSavingPermissions(false);
    }
  }

  if (loading) {
    return (
      <PageShell title={uiText.admin.groupManagement.title}>
        <PageState state="loading" />
      </PageShell>
    );
  }

  if (!group) {
    return (
      <PageShell title={uiText.admin.groupManagement.title}>
        <PageState state="error" />
        {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}
      </PageShell>
    );
  }

  return (
    <PageShell
      title={group.name}
      description={uiText.admin.groupManagement.description}
      secondaryActions={
        <Link href="/admin/groups" className="hover:underline">
          {uiText.admin.groupManagement.backToList}
        </Link>
      }
    >
      <section className="rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4">
        <h2 className="mb-3 text-lg font-medium">{uiText.admin.groupManagement.membersTitle}</h2>
        <ul className="mb-4 max-h-64 space-y-2 overflow-y-auto">
          {users.map((user) => {
            const inputId = checkboxInputId("group-member", user.userId);
            const labelText = memberCheckboxLabel(user, uiText.admin.users.inactive);
            return (
              <li key={user.userId} className="flex items-center gap-2 text-sm">
                <input
                  id={inputId}
                  type="checkbox"
                  checked={selectedUserIds.has(user.userId)}
                  onChange={() => toggleUser(user.userId)}
                />
                <label htmlFor={inputId} className="cursor-pointer">
                  {labelText}
                </label>
              </li>
            );
          })}
        </ul>
        <button
          type="button"
          disabled={savingMembers}
          onClick={() => {
            void saveMembers();
          }}
          className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {savingMembers ? uiText.admin.groupManagement.saving : uiText.admin.groupManagement.saveMembers}
        </button>
      </section>

      <section className="rounded-xl border border-[var(--md-sys-color-outline)] bg-[var(--md-sys-color-surface)] p-4">
        <h2 className="mb-3 text-lg font-medium">{uiText.admin.groupManagement.permissionsTitle}</h2>
        <ul className="mb-4 max-h-64 space-y-2 overflow-y-auto">
          {assignablePermissions.map((permission) => {
            const inputId = checkboxInputId("group-permission", permission.permissionKey);
            const labelText = `${permission.displayLabel} (${permission.permissionKey})`;
            return (
              <li key={permission.permissionKey} className="flex items-start gap-2 text-sm">
                <input
                  id={inputId}
                  type="checkbox"
                  checked={selectedPermissionKeys.has(permission.permissionKey)}
                  onChange={() => togglePermission(permission.permissionKey)}
                />
                <label htmlFor={inputId} className="cursor-pointer">
                  {labelText}
                </label>
              </li>
            );
          })}
        </ul>
        <button
          type="button"
          disabled={savingPermissions}
          onClick={() => {
            void savePermissions();
          }}
          className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-800 disabled:opacity-60"
        >
          {savingPermissions ? uiText.admin.groupManagement.saving : uiText.admin.groupManagement.savePermissions}
        </button>
      </section>

      {toast ? <Toast toast={toast} onClose={() => setToast(null)} /> : null}
    </PageShell>
  );
}
