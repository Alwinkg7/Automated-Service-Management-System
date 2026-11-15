"use client";
import { useState } from "react";
import { toast } from "sonner";
import { browserApi } from "@/lib/api";
import { errorMessage } from "@/lib/api/errors";
import { saveBlob } from "@/lib/format";

/** Download a file (PDF/CSV) through the authenticated proxy. */
export function useDownload() {
  const [pending, setPending] = useState(false);
  async function download(path: string, fallbackName: string, query?: Record<string, string | number | undefined>) {
    setPending(true);
    try {
      const { blob, fileName } = await browserApi.download(path, { query });
      saveBlob(blob, fileName ?? fallbackName);
    } catch (error) {
      toast.error(errorMessage(error));
    } finally {
      setPending(false);
    }
  }
  return { download, pending };
}
