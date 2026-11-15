import { ButtonLink } from "@/components/ui/Button";

export default function NotFound() {
  return (
    <main
      id="main"
      className="flex min-h-[70dvh] flex-col items-center justify-center px-4 text-center"
    >
      <p className="text-primary text-sm font-semibold">404</p>
      <h1 className="mt-2 text-2xl font-semibold">Page not found</h1>
      <p className="text-fg-muted mt-2">
        The page you&apos;re looking for doesn&apos;t exist or has moved.
      </p>
      <ButtonLink href="/" className="mt-6">
        Back to home
      </ButtonLink>
    </main>
  );
}
