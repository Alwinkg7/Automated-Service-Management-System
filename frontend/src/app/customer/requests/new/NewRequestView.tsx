"use client";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { PageHeader } from "@/components/layout/PageHeader";
import { SkeletonList } from "@/components/ui/Skeleton";
import { ErrorState } from "@/components/ui/States";
import { customerApi } from "@/lib/api";
import { qk } from "@/lib/queryKeys";
import { SERVICE_CATEGORIES, type ServiceCategory } from "@/types/enums";
import { BookingForm } from "./BookingForm";

/** Book a new request, or re-book a past one (?rebook=ID) with its details pre-filled. */
export function NewRequestView() {
  const params = useSearchParams();
  const rebookId = Number(params.get("rebook")) || undefined;
  const categoryParam = params.get("category");
  const initialCategory = SERVICE_CATEGORIES.includes(categoryParam as ServiceCategory)
    ? (categoryParam as ServiceCategory)
    : undefined;

  const original = useQuery({
    queryKey: qk.customer.request(rebookId ?? 0),
    queryFn: () => customerApi.getRequest(rebookId!),
    enabled: !!rebookId,
  });

  return (
    <>
      <PageHeader
        title={rebookId ? "Re-book a service" : "Book a service"}
        description={
          rebookId
            ? "We've filled in the details from your previous request. Pick a new time and confirm."
            : "Tell us what you need and when. We'll match you with an available technician."
        }
        back={{ href: rebookId ? "/customer/history" : "/customer/requests", label: "Back" }}
      />
      {rebookId && original.isPending ? (
        <SkeletonList rows={3} />
      ) : rebookId && original.isError ? (
        <ErrorState error={original.error} onRetry={() => void original.refetch()} />
      ) : (
        <BookingForm
          key={rebookId ?? "new"}
          defaults={
            original.data
              ? {
                  category: original.data.category,
                  description: original.data.description,
                  address: original.data.address,
                  pinCode: original.data.pinCode ?? "",
                }
              : { category: initialCategory }
          }
          previousTechnician={original.data?.technician?.name ?? undefined}
        />
      )}
    </>
  );
}
