"use client";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/layout/PageHeader";
import { UserTable } from "@/components/features/admin/UserTable";
import { FilterChips } from "@/components/ui/FilterChips";
import { QueryView } from "@/components/ui/QueryView";
import { SearchInput } from "@/components/ui/SearchInput";
import { EmptyState } from "@/components/ui/States";
import { useDebounce } from "@/hooks/useDebounce";
import { useUrlState } from "@/hooks/useUrlState";
import { adminApi } from "@/lib/api";
import { qk } from "@/lib/queryKeys";
import { USER_ROLES, type UserRole } from "@/types/enums";

export function UsersView() {
  const url = useUrlState();
  const role = url.get("role") as UserRole | "";
  const search = url.get("q");
  const term = useDebounce(search.trim().toLowerCase());
  const query = useQuery({
    queryKey: qk.admin.users(role),
    queryFn: () => adminApi.getUsers(role),
  });
  const rows = (query.data?.items ?? []).filter(
    (u) => !term || [u.name, u.email, u.phone].some((v) => v?.toLowerCase().includes(term)),
  );

  return (
    <>
      <PageHeader
        title="Users"
        description="Customers, technicians and admins. Deactivated users can't sign in."
      />
      <div className="mb-4 space-y-3">
        <SearchInput
          value={search}
          onChange={(q) => url.set({ q })}
          placeholder="Search name, email or phone"
        />
        <FilterChips
          label="Filter by role"
          value={role}
          onChange={(r) => url.set({ role: r })}
          options={[
            { value: "", label: "All" },
            ...USER_ROLES.map((r) => ({ value: r, label: `${r}s` })),
          ]}
        />
      </div>
      <QueryView
        query={query}
        isEmpty={() => rows.length === 0}
        empty={<EmptyState title="No users found" />}
      >
        {() => <UserTable users={rows} showRole={!role} />}
      </QueryView>
    </>
  );
}
