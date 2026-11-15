import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api/errors";
import { LoginForm } from "./LoginForm";

const replace = vi.fn();
const refresh = vi.fn();
let search = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace, refresh }),
  useSearchParams: () => search,
}));

const login = vi.fn();
vi.mock("@/lib/api/auth-client", () => ({ login: (...args: unknown[]) => login(...args) }));

async function fillAndSubmit(email = "asha@example.com", password = "Secret123") {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText(/email/i), email);
  await user.type(screen.getByLabelText(/password/i), password);
  await user.click(screen.getByRole("button", { name: /sign in/i }));
}

describe("LoginForm", () => {
  beforeEach(() => {
    replace.mockReset();
    refresh.mockReset();
    login.mockReset();
    search = new URLSearchParams();
  });

  it("validates before calling the API", async () => {
    render(<LoginForm />);
    await userEvent.setup().click(screen.getByRole("button", { name: /sign in/i }));
    expect(await screen.findByText("Enter a valid email")).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it("redirects to the role dashboard after login", async () => {
    login.mockResolvedValue({ user: { id: "1", role: "Technician", name: "T", email: "t@x.io" } });
    render(<LoginForm />);
    await fillAndSubmit();
    expect(login).toHaveBeenCalledWith("asha@example.com", "Secret123");
    expect(replace).toHaveBeenCalledWith("/technician/dashboard");
  });

  it("honours a safe ?next= but ignores another role's area", async () => {
    search = new URLSearchParams({ next: "/admin/users" });
    login.mockResolvedValue({ user: { id: "1", role: "Customer", name: "C", email: "c@x.io" } });
    render(<LoginForm />);
    await fillAndSubmit();
    expect(replace).toHaveBeenCalledWith("/customer/dashboard");
  });

  it("shows the server message on bad credentials", async () => {
    login.mockRejectedValue(new ApiError({ status: 401, message: "Invalid credentials." }));
    render(<LoginForm />);
    await fillAndSubmit();
    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid credentials.");
    expect(replace).not.toHaveBeenCalled();
  });
});
