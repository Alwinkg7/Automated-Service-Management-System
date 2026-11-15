"use client";
// Loads Razorpay Checkout on demand (only on the pay page) and wraps it in a promise.

export interface RazorpaySuccess {
  razorpay_payment_id: string;
  razorpay_order_id: string;
  razorpay_signature: string;
}

interface RazorpayOptions {
  key: string;
  amount: number;
  currency: string;
  order_id: string;
  name: string;
  description?: string;
  prefill?: { name?: string; email?: string; contact?: string };
  theme?: { color?: string };
  handler: (response: RazorpaySuccess) => void;
  modal?: { ondismiss?: () => void };
}

interface RazorpayInstance {
  open: () => void;
  on: (event: "payment.failed", cb: (e: { error?: { description?: string } }) => void) => void;
}

declare global {
  interface Window {
    Razorpay?: new (options: RazorpayOptions) => RazorpayInstance;
  }
}

const SCRIPT_SRC = "https://checkout.razorpay.com/v1/checkout.js";
let loading: Promise<void> | null = null;

export function loadRazorpay(): Promise<void> {
  if (typeof window === "undefined") return Promise.reject(new Error("Razorpay needs a browser"));
  if (window.Razorpay) return Promise.resolve();
  loading ??= new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = SCRIPT_SRC;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => {
      loading = null;
      reject(new Error("Could not load the payment window. Check your connection."));
    };
    document.body.appendChild(script);
  });
  return loading;
}

export class PaymentCancelledError extends Error {
  constructor(message = "Payment was cancelled.") {
    super(message);
    this.name = "PaymentCancelledError";
  }
}

/** Opens Checkout and resolves with the ids the backend must verify. */
export async function openCheckout(options: Omit<RazorpayOptions, "handler" | "modal">): Promise<RazorpaySuccess> {
  await loadRazorpay();
  const Razorpay = window.Razorpay;
  if (!Razorpay) throw new Error("Payment window unavailable.");
  return new Promise((resolve, reject) => {
    const rzp = new Razorpay({
      ...options,
      handler: resolve,
      modal: { ondismiss: () => reject(new PaymentCancelledError()) },
    });
    rzp.on("payment.failed", (e) => reject(new PaymentCancelledError(e.error?.description ?? "Payment failed.")));
    rzp.open();
  });
}
