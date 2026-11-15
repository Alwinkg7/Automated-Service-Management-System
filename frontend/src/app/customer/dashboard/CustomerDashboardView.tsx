"use client";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/components/layout/PageHeader";
import { useSession } from "@/components/providers/SessionProvider";
import { RequestCard } from "@/components/features/request/RequestCard";
import { ButtonLink } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { Icon } from "@/components/ui/Icon";
import { QueryView } from "@/components/ui/QueryView";
import { SkeletonList, SkeletonStats } from "@/components/ui/Skeleton";
import { StatCard, StatGrid } from "@/components/ui/StatCard";
import { EmptyState } from "@/components/ui/States";
import { customerApi } from "@/lib/api";
import { qk } from "@/lib/queryKeys";

export function CustomerDashboardView() {
  const user = useSession();
  const query = useQuery({ queryKey: qk.customer.dashboard(), queryFn: customerApi.getDashboard });

  return (
    <>
      <PageHeader
        title={`Hello${user.name ? `, ${user.name.split(" ")[0]}` : ""}`}
        description="Here's what's happening with your service requests."
        actions={
          <ButtonLink href="/customer/requests/new" icon={<Icon name="plus" className="size-4" />}>
            Book a service
          </ButtonLink>
        }
      />
      <QueryView
        query={query}
        loading={
          <div className="space-y-6">
            <SkeletonStats />
            <SkeletonList rows={3} />
          </div>
        }
      >
        {(data) => (
          <div className="space-y-6">
            <StatGrid>
              <StatCard label="Total requests" value={data.stats.total} icon="list" />
              <StatCard label="Pending" value={data.stats.pending} icon="history" />
              <StatCard label="In progress" value={data.stats.inProgress} icon="wrench" />
              <StatCard label="Completed" value={data.stats.completed} icon="check" />
            </StatGrid>
            <Card>
              <CardHeader
                title="Recent requests"
                action={
                  <ButtonLink href="/customer/requests" variant="ghost" size="sm">
                    View all
                  </ButtonLink>
                }
              />
              <CardBody>
                {data.recentRequests.length === 0 ? (
                  <EmptyState
                    title="No requests yet"
                    description="Book your first service and we'll match you with a technician."
                    action={<ButtonLink href="/customer/requests/new">Book a service</ButtonLink>}
                  />
                ) : (
                  <ul className="space-y-3">
                    {data.recentRequests.map((r) => (
                      <li key={r.id}>
                        <RequestCard request={r} />
                      </li>
                    ))}
                  </ul>
                )}
              </CardBody>
            </Card>
          </div>
        )}
      </QueryView>
    </>
  );
}
