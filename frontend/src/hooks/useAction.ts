"use client";
// useMutation + toast + cache invalidation in one place, so pages stay small.
import { useMutation, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { toast } from "sonner";
import { errorMessage, isApiError } from "@/lib/api/errors";

interface ActionOptions<TVars, TResult> {
  fn: (vars: TVars) => Promise<TResult>;
  /** Query keys to refetch after success (and after 409, since state changed). */
  invalidate?: QueryKey[];
  success?: string | ((result: TResult, vars: TVars) => string | undefined);
  onSuccess?: (result: TResult, vars: TVars) => void;
  /** Return true to skip the default error toast (e.g. form shows the error). */
  onError?: (error: unknown) => boolean | void;
}

export function useAction<TVars = void, TResult = unknown>(options: ActionOptions<TVars, TResult>) {
  const queryClient = useQueryClient();
  const refresh = () =>
    Promise.all((options.invalidate ?? []).map((queryKey) => queryClient.invalidateQueries({ queryKey })));

  return useMutation({
    mutationFn: options.fn,
    onSuccess: async (result, vars) => {
      await refresh();
      const message = typeof options.success === "function" ? options.success(result, vars) : options.success;
      if (message) toast.success(message);
      options.onSuccess?.(result, vars);
    },
    onError: (error) => {
      // A conflict means the server state moved on – refetch so the UI catches up.
      if (isApiError(error) && error.isConflict) void refresh();
      if (options.onError?.(error)) return;
      if (isApiError(error) && error.isUnauthorized) return; // redirecting to login
      toast.error(errorMessage(error));
    },
  });
}
