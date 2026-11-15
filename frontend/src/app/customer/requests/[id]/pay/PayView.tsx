"use client";
// Payment flow: create order (server computes the amount from the bill) →
// Razorpay Checkout → send the ids back for server-side signature verification.
// The frontend never decides the amount and never marks a bill paid itself.
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { PageHeader } from "@/components/layout/PageHeader";
import { useSession } from "@/components/providers/SessionProvider";
import { BillSummary } from "@/components/features/bill/BillSummary";
import { Button, ButtonLink } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { FormError, Input } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { QueryView } from "@/components/ui/QueryView";
import { EmptyState } from "@/components/ui/States";
import { customerApi } from "@/lib/api";
import { errorMessage } from "@/lib/api/errors";
import { formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { openCheckout, PaymentCancelledError } from "@/lib/razorpay";

type Step = "idle" | "creating" | "checkout" | "verifying";

export function PayView({ requestId }: { requestId: number }) {
  const user = useSession();
  const router = useRouter();
  const queryClient = useQueryClient();
  const request = useQuery({
    queryKey: qk.customer.request(requestId),
    queryFn: () => customerApi.getRequest(requestId),
  });
  const promotions = useQuery({
    queryKey: qk.customer.promotions(),
    queryFn: customerApi.getPromotions,
    retry: false,
  });

  const [step, setStep] = useState<Step>("idle");
  const [error, setError] = useState<string | null>(null);
  const [promoCode, setPromoCode] = useState("");
  const [promo, setPromo] = useState<{
    code: string;
    discount: number;
    message?: string | null;
  } | null>(null);
  const [promoError, setPromoError] = useState<string | null>(null);
  const [checkingPromo, setCheckingPromo] = useState(false);
  const [redeemPoints, setRedeemPoints] = useState(false);
  // Same key for retries of the same order attempt; reset after a finished attempt.
  const orderKey = useRef<string>(crypto.randomUUID());

  async function checkPromo(amount: number) {
    setPromoError(null);
    setCheckingPromo(true);
    try {
      const result = await customerApi.validatePromo(promoCode.trim(), amount);
      setPromo({
        code: promoCode.trim().toUpperCase(),
        discount: result.discount,
        message: result.message,
      });
    } catch (e) {
      setPromo(null);
      setPromoError(errorMessage(e));
    } finally {
      setCheckingPromo(false);
    }
  }

  async function pay(billId: number) {
    setError(null);
    setStep("creating");
    try {
      const order = await customerApi.createRazorpayOrder(
        billId,
        { promoCode: promo?.code, redeemPoints: redeemPoints || undefined },
        orderKey.current,
      );
      setStep("checkout");
      const result = await openCheckout({
        key: order.keyId, // public key id from the API; never hard-coded
        amount: order.amount,
        currency: order.currency,
        order_id: order.orderId,
        name: "ASMS",
        description: `Bill #${billId} for request #${requestId}`,
        prefill: { name: user.name, email: user.email },
        theme: { color: "#2453d6" },
      });
      setStep("verifying");
      await customerApi.verifyRazorpayPayment(billId, {
        razorpayOrderId: result.razorpay_order_id,
        razorpayPaymentId: result.razorpay_payment_id,
        razorpaySignature: result.razorpay_signature,
      });
      orderKey.current = crypto.randomUUID();
      await queryClient.invalidateQueries({ queryKey: qk.customer.all });
      toast.success("Payment successful! Your request is now complete.");
      router.replace(`/customer/requests/${requestId}`);
    } catch (e) {
      setStep("idle");
      if (e instanceof PaymentCancelledError) {
        setError(e.message);
        return;
      }
      setError(errorMessage(e));
      // 409 (already paid / state changed): show fresh data.
      void queryClient.invalidateQueries({ queryKey: qk.customer.request(requestId) });
    }
  }

  const busy = step !== "idle";
  const label =
    step === "creating"
      ? "Preparing payment…"
      : step === "checkout"
        ? "Complete payment in the window…"
        : step === "verifying"
          ? "Confirming payment…"
          : null;

  return (
    <QueryView query={request}>
      {(r) => {
        const bill = r.bill;
        if (!bill?.id) {
          return (
            <EmptyState
              title="No bill yet"
              description="The technician hasn't created a bill for this request."
              action={<ButtonLink href={`/customer/requests/${requestId}`}>Back</ButtonLink>}
            />
          );
        }
        if (bill.isPaid) {
          return (
            <EmptyState
              title="Already paid"
              description="This bill has been paid. Thank you!"
              action={
                <ButtonLink href={`/customer/requests/${requestId}`}>View request</ButtonLink>
              }
            />
          );
        }
        const points = promotions.data?.pointsBalance ?? 0;
        return (
          <>
            <PageHeader
              back={{ href: `/customer/requests/${requestId}`, label: `Request #${requestId}` }}
              title="Pay your bill"
            />
            <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
              <Card>
                <CardHeader title={`Bill #${bill.id}`} />
                <CardBody>
                  <BillSummary bill={bill} />
                </CardBody>
              </Card>

              <div className="space-y-4">
                {/* Only shown once the API exposes promotions (see docs/api-requests.md). */}
                {promotions.isSuccess ? (
                  <Card>
                    <CardHeader
                      title="Discounts"
                      description="Applied by the server when your order is created."
                    />
                    <CardBody className="space-y-4">
                      <div className="space-y-2">
                        <label htmlFor="promo" className="text-sm font-medium">
                          Promo code
                        </label>
                        <div className="flex gap-2">
                          <Input
                            id="promo"
                            value={promoCode}
                            onChange={(e) => setPromoCode(e.target.value)}
                            placeholder="e.g. WELCOME100"
                            disabled={busy}
                          />
                          <Button
                            variant="outline"
                            onClick={() => void checkPromo(bill.amount)}
                            disabled={!promoCode.trim() || busy}
                            loading={checkingPromo}
                          >
                            Apply
                          </Button>
                        </div>
                        {promo ? (
                          <p className="text-success text-sm" role="status">
                            {promo.message ??
                              `${promo.code}: you save ${formatMoney(promo.discount)}`}
                          </p>
                        ) : null}
                        {promoError ? (
                          <p className="text-danger text-sm" role="alert">
                            {promoError}
                          </p>
                        ) : null}
                      </div>
                      {points >= 100 ? (
                        <label className="flex items-start gap-2 text-sm">
                          <input
                            type="checkbox"
                            className="accent-primary mt-0.5 size-4"
                            checked={redeemPoints}
                            onChange={(e) => setRedeemPoints(e.target.checked)}
                            disabled={busy}
                          />
                          <span>
                            Redeem loyalty points{" "}
                            <span className="text-fg-muted">
                              ({points} pts available, 100 pts = ₹10)
                            </span>
                          </span>
                        </label>
                      ) : null}
                    </CardBody>
                  </Card>
                ) : null}

                <Card>
                  <CardBody className="space-y-3">
                    <FormError message={error} />
                    <p className="text-fg-muted text-sm">
                      The final amount is confirmed in the secure Razorpay window.
                    </p>
                    <Button
                      className="w-full"
                      size="lg"
                      loading={busy}
                      onClick={() => void pay(bill.id!)}
                      icon={<Icon name="card" className="size-4" />}
                    >
                      {label ?? `Pay ${formatMoney(bill.amount)}`}
                    </Button>
                    <p className="text-fg-muted flex items-center justify-center gap-1.5 text-xs">
                      <Icon name="shield" className="size-3.5" /> Secured by Razorpay
                    </p>
                  </CardBody>
                </Card>
              </div>
            </div>
          </>
        );
      }}
    </QueryView>
  );
}
