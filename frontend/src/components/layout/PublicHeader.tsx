import Link from "next/link";
import { ButtonLink } from "@/components/ui/Button";
import { Logo } from "./Logo";
import { ThemeToggle } from "./ThemeToggle";

export function PublicHeader() {
  return (
    <header className="border-border bg-bg/85 sticky top-0 z-30 border-b backdrop-blur">
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4 sm:px-6">
        <Logo />
        <nav aria-label="Main" className="text-fg-muted ml-6 hidden gap-6 text-sm sm:flex">
          <Link href="/services" className="hover:text-fg">
            Services
          </Link>
          <Link href="/#how-it-works" className="hover:text-fg">
            How it works
          </Link>
          <Link href="/register?role=Technician" className="hover:text-fg">
            Become a technician
          </Link>
        </nav>
        <div className="ml-auto flex items-center gap-2">
          <ThemeToggle />
          <ButtonLink href="/login" variant="ghost" size="sm">
            Sign in
          </ButtonLink>
          <ButtonLink href="/register" size="sm" className="hidden sm:inline-flex">
            Get started
          </ButtonLink>
        </div>
      </div>
    </header>
  );
}

export function PublicFooter() {
  return (
    <footer className="border-border border-t">
      <div className="text-fg-muted mx-auto flex max-w-6xl flex-col gap-4 px-4 py-8 text-sm sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <p>© {new Date().getFullYear()} ASMS – Automated Service Management System</p>
        <nav aria-label="Footer" className="flex gap-5">
          <Link href="/services" className="hover:text-fg">
            Services
          </Link>
          <Link href="/login" className="hover:text-fg">
            Sign in
          </Link>
          <Link href="/register" className="hover:text-fg">
            Create account
          </Link>
        </nav>
      </div>
    </footer>
  );
}
