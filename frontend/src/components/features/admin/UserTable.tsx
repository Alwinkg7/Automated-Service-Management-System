"use client";
import { useState } from "react";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { DataTable } from "@/components/ui/DataTable";
import { useAction } from "@/hooks/useAction";
import { adminApi } from "@/lib/api";
import type { AdminUser } from "@/lib/api/schemas";
import { formatDate } from "@/lib/format";
import { qk } from "@/lib/queryKeys";

/** Users with activate/deactivate (toggle-active). Shared by Users and Technicians pages. */
export function UserTable({ users, showRole = true }: { users: AdminUser[]; showRole?: boolean }) {
  const [target, setTarget] = useState<AdminUser | null>(null);
  const toggle = useAction({
    fn: (id: string) => adminApi.toggleUserActive(id),
    invalidate: [qk.admin.all],
    success: (r) => (r?.isActive === undefined ? "Updated." : r.isActive ? "Account activated." : "Account deactivated."),
    onSuccess: () => setTarget(null),
  });

  return (
    <>
      <DataTable
        caption="Users"
        rows={users}
        rowKey={(u) => u.id}
        columns={[
          {
            header: "Name",
            cell: (u) => (
              <span>
                <span className="block font-medium">{u.name ?? "—"}</span>
                <span className="text-xs text-fg-muted">{u.email}</span>
              </span>
            ),
          },
          ...(showRole ? [{ header: "Role", cell: (u: AdminUser) => <Badge tone="primary">{u.role}</Badge> }] : []),
          { header: "Phone", cell: (u) => u.phone ?? "—", hideOnMobile: true },
          { header: "Joined", cell: (u) => formatDate(u.createdAt), hideOnMobile: true },
          { header: "Status", cell: (u) => <StatusBadge status={u.isActive ? "Active" : "Inactive"} /> },
        ]}
        rowAction={(u) => (
          <Button size="sm" variant={u.isActive ? "outline" : "secondary"} onClick={() => setTarget(u)}>
            {u.isActive ? "Deactivate" : "Activate"}
          </Button>
        )}
      />
      <ConfirmDialog
        open={!!target}
        title={target?.isActive ? `Deactivate ${target?.name ?? "user"}?` : `Activate ${target?.name ?? "user"}?`}
        description={
          target?.isActive
            ? "They will be signed out and can't sign in until reactivated."
            : "They will be able to sign in again."
        }
        confirmLabel={target?.isActive ? "Deactivate" : "Activate"}
        tone={target?.isActive ? "danger" : "primary"}
        loading={toggle.isPending}
        onConfirm={() => target && toggle.mutate(target.id)}
        onClose={() => setTarget(null)}
      />
    </>
  );
}
