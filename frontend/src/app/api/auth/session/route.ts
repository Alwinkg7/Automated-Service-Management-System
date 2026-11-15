import { NextResponse } from "next/server";
import { getSession } from "@/lib/auth/server";

export async function GET() {
  const user = await getSession();
  if (!user) return NextResponse.json({ success: false, error: "Not signed in." }, { status: 401 });
  return NextResponse.json(
    { success: true, data: { id: user.id, name: user.name, email: user.email, role: user.role } },
    { headers: { "Cache-Control": "no-store" } },
  );
}
