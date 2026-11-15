import { notFound } from "next/navigation";

/** Parse a numeric route id or render the 404 page. */
export async function numericId(params: Promise<{ id: string }>): Promise<number> {
  const { id } = await params;
  const n = Number(id);
  if (!Number.isInteger(n) || n <= 0) notFound();
  return n;
}
