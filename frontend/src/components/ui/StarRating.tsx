"use client";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

/** Radio-group star picker (keyboard accessible). Read-only when no onChange. */
export function StarRating({
  value,
  onChange,
  label = "Rating",
}: {
  value: number;
  onChange?: (v: number) => void;
  label?: string;
}) {
  return (
    <div
      role={onChange ? "radiogroup" : "img"}
      aria-label={onChange ? label : `${value} out of 5 stars`}
      className="flex gap-1"
    >
      {[1, 2, 3, 4, 5].map((n) =>
        onChange ? (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={value === n}
            aria-label={`${n} star${n > 1 ? "s" : ""}`}
            onClick={() => onChange(n)}
            className="rounded p-0.5"
          >
            <Icon
              name="star"
              className={cn("size-7", n <= value ? "fill-warning text-warning" : "text-fg-muted")}
            />
          </button>
        ) : (
          <Icon
            key={n}
            name="star"
            className={cn("size-4", n <= value ? "fill-warning text-warning" : "text-fg-muted")}
          />
        ),
      )}
    </div>
  );
}
