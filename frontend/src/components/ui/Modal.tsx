"use client";
// Accessible modal built on the native <dialog> element (focus trap + Esc for free).
import { useEffect, useId, useRef, type ReactNode } from "react";
import { cn } from "@/lib/cn";
import { Icon } from "./Icon";

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  size?: "sm" | "md" | "lg";
}

const widths = { sm: "max-w-sm", md: "max-w-lg", lg: "max-w-2xl" };

export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  size = "md",
}: ModalProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) dialog.showModal();
    if (!open && dialog.open) dialog.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      onClose={onClose}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
      // Click on the backdrop closes the dialog.
      onClick={(e) => {
        if (e.target === ref.current) onClose();
      }}
      className={cn(
        "rounded-card border-border bg-surface text-fg m-auto w-[calc(100%-2rem)] border p-0 shadow-xl",
        "open:animate-slide-up backdrop:bg-black/50",
        widths[size],
      )}
    >
      {open ? (
        <div className="flex max-h-[85vh] flex-col">
          <header className="border-border flex items-start justify-between gap-4 border-b px-5 py-4">
            <div>
              <h2 id={titleId} className="text-lg font-semibold">
                {title}
              </h2>
              {description ? <p className="text-fg-muted mt-1 text-sm">{description}</p> : null}
            </div>
            <button
              type="button"
              onClick={onClose}
              className="text-fg-muted hover:bg-surface-muted rounded-md p-1"
              aria-label="Close"
            >
              <Icon name="close" />
            </button>
          </header>
          <div className="overflow-y-auto px-5 py-4">{children}</div>
          {footer ? (
            <footer className="border-border flex justify-end gap-2 border-t px-5 py-3">
              {footer}
            </footer>
          ) : null}
        </div>
      ) : null}
    </dialog>
  );
}
