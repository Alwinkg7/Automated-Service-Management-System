import type { Key, ReactNode } from "react";
import { cn } from "@/lib/cn";

export interface Column<T> {
  header: string;
  cell: (row: T) => ReactNode;
  className?: string;
  /** Hide in the stacked mobile card (e.g. secondary info). */
  hideOnMobile?: boolean;
}

/**
 * Responsive table: a real <table> from md up, stacked cards on phones.
 * Pass `rowAction` to render a trailing action cell (buttons/links).
 */
export function DataTable<T>({
  rows,
  columns,
  rowKey,
  rowAction,
  caption,
}: {
  rows: T[];
  columns: Column<T>[];
  rowKey: (row: T) => Key;
  rowAction?: (row: T) => ReactNode;
  caption?: string;
}) {
  return (
    <div className="rounded-card border-border bg-surface border">
      <table className="hidden w-full text-left text-sm md:table">
        {caption ? <caption className="sr-only">{caption}</caption> : null}
        <thead className="border-border text-fg-muted border-b text-xs tracking-wide uppercase">
          <tr>
            {columns.map((c) => (
              <th key={c.header} scope="col" className={cn("px-4 py-3 font-medium", c.className)}>
                {c.header}
              </th>
            ))}
            {rowAction ? (
              <th scope="col" className="px-4 py-3">
                <span className="sr-only">Actions</span>
              </th>
            ) : null}
          </tr>
        </thead>
        <tbody className="divide-border divide-y">
          {rows.map((row) => (
            <tr key={rowKey(row)} className="hover:bg-surface-muted/50">
              {columns.map((c) => (
                <td key={c.header} className={cn("px-4 py-3 align-middle", c.className)}>
                  {c.cell(row)}
                </td>
              ))}
              {rowAction ? (
                <td className="px-4 py-3 text-right whitespace-nowrap">{rowAction(row)}</td>
              ) : null}
            </tr>
          ))}
        </tbody>
      </table>

      <ul className="divide-border divide-y md:hidden" aria-label={caption}>
        {rows.map((row) => (
          <li key={rowKey(row)} className="space-y-2 p-4">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
              {columns
                .filter((c) => !c.hideOnMobile)
                .map((c) => (
                  <div key={c.header} className="contents">
                    <dt className="text-fg-muted">{c.header}</dt>
                    <dd className="min-w-0 text-right break-words">{c.cell(row)}</dd>
                  </div>
                ))}
            </dl>
            {rowAction ? (
              <div className="flex flex-wrap justify-end gap-2 pt-1">{rowAction(row)}</div>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}
