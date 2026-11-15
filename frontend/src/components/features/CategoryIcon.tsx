import { Icon, type IconName } from "@/components/ui/Icon";
import { cn } from "@/lib/cn";
import type { ServiceCategory } from "@/types/enums";

const ICONS: Record<ServiceCategory, IconName> = {
  Plumbing: "wrench",
  Electrical: "sun",
  Carpentry: "briefcase",
  Cleaning: "refresh",
  AC_Repair: "zone",
  Appliance_Repair: "card",
  Painting: "tag",
  General: "home",
};

export function CategoryIcon({
  category,
  className,
}: {
  category: ServiceCategory;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "bg-primary-soft text-primary inline-flex size-10 items-center justify-center rounded-xl",
        className,
      )}
    >
      <Icon name={ICONS[category]} />
    </span>
  );
}
