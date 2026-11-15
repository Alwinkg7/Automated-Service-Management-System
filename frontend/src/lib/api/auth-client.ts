"use client";
// Calls to our own /api/auth/* route handlers (same origin, cookies set server-side).
import type { Role } from "@/lib/auth/constants";
import type { RegisterForm } from "@/lib/validation/auth";
import { createApiClient } from "./client";

const local = createApiClient({ baseUrl: "" });

export function login(email: string, password: string) {
  return local.post<{ user: { id: string; role: Role; name: string; email: string } }>("/api/auth/login", {
    email,
    password,
  });
}

export function registerAccount(form: RegisterForm) {
  return local.post<{ role: Role; requiresLogin?: boolean }>("/api/auth/register", form);
}
