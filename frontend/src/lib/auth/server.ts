import "server-only";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { TOKEN_COOKIE, type Role } from "./constants";
import { decodeJwt, isExpired, type SessionUser } from "./jwt";

/** Current user from the token cookie, or null. */
export async function getSession(): Promise<SessionUser | null> {
  const token = (await cookies()).get(TOKEN_COOKIE)?.value;
  const user = decodeJwt(token);
  if (!user || isExpired(user)) return null;
  return user;
}

/** Use in role layouts: redirects when signed out or on the wrong role. */
export async function requireRole(role: Role): Promise<SessionUser> {
  const user = await getSession();
  if (!user) redirect("/login");
  if (user.role !== role) redirect("/403");
  return user;
}
