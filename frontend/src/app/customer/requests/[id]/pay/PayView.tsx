"use client";
// Payment: the server owns every amount.
//   quote → (optional) promo preview/apply, points → Razorpay order (payable, in paise)
//   → Checkout → server-side signature verification.
// Or the customer chooses cash and the technician confirms collection.
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { PageHeader } from "@/components/layout/PageHeader";
import { useSession } from "@/components/providers/SessionProvider";
import { BillSummary } from "@/components/features/bill/BillSummary";
import { Button, ButtonLink } from "@/components/ui/Button";
import { Card, CardBody, CardHeader } from "@/components/ui/Card";
import { FormError, Input, Select } from "@/components/ui/Field";
import { Icon } from "@/components/ui/Icon";
import { QueryView } from "@/components/ui/QueryView";
import { Skeleton } from "@/components/ui/Skeleton";
import { EmptyState } from "@/components/ui/States";
import { useAction } from "@/hooks/useAction";
import { customerApi } from "@/lib/api";
import { errorMessage } from "@/lib/api/errors";
import type { Quote } from "@/lib/api/schemas";
import { formatMoney } from "@/lib/format";
import { qk } from "@/lib/queryKeys";
import { openCheckout, PaymentCancelledError } from "@/lib/razorpay";

type Step = "idle" | "creating" | "checkout" | "verifying";

function newKey() {
  return crypto.randomUUID();
}

function QuoteLines({ quote }: { quote: Quote }) {
  return (
    <dl className="space-y-1.5 text-sm">
      <div className="flex justify-between">
        <dt className="text-fg-muted">Subtotal</dt>
        <dd className="tabular-nums">{formatMoney(quote.subtotal)}</dd>
      </div>
      {quote.discount > 0 ? (
        <div className="text-success flex justify-between">
          <dt>
            Discount
            {quote.promoCode ? ` (${quote.promoCode})` : ""}
            {quote.pointsRedeemed ? ` · ${quote.pointsRedeemed} pts` : ""}
          </dt>
          <dd className="tabular-nums">−{formatMoney(quote.discount)}</dd>
        </div>
      ) : null}
      <div className="border-border flex justify-between border-t pt-2 text-base font-semibold">
        <dt>To pay</dt>
        <dd className="tabular-nums">{formatMoney(quote.payable)}</dd>
      </div>
    </dl>
  );
}

function Discounts({ billId, quote }: { billId: number; quote: Quote }) {
  const queryClient = useQueryClient();
  const promotions = useQuery({
    queryKey: qk.customer.promotions(),
    queryFn: customerApi.getPromotions,
  });
  const [code, setCode] = useState("");
  const [preview, setPreview] = useState<Quote | null>(null);
  const [points, setPoints] = useState("");
  const setQuote = (q: Quote) => {
    queryClient.setQueryData(qk.customer.quote(billId), q);
    void queryClient.invalidateQueries({ queryKey: qk.customer.promotions() });
  };

  const check = useAction({
    fn: () => customerApi.previewPromo(billId, code.trim()),
    onSuccess: (q) => setPreview(q),
    onError: () => setPreview(null),
  });
  const apply = useAction({
    fn: () => customerApi.applyPromo(billId, code.trim()),
    success: "Promo code applied.",
    onSuccess: (q) => {
      setQuote(q);
      setPreview(null);
      setCode("");
    },
  });
  const redeem = useAction({
    fn: () => customerApi.redeemPoints(billId, Number(points)),
    success: "Points redeemed.",
    onSuccess: (q) => {
      setQuote(q);
      setPoints("");
    },
  });

  const balance = promotions.data?.pointsBalance ?? 0;
  // Multiples of 100, and at most (payable − ₹1) worth (100 pts = ₹10).
  const maxPoints = Math.min(
    Math.floor(balance / 100) * 100,
    Math.floor(((quote.payable - 1) * 10) / 100) * 100,
  );
  const pointOptions = Array.from(
    { length: Math.max(0, maxPoints / 100) },
    (_, i) => (i + 1) * 100,
  );

  return (
    <Card>
      <CardHeader title="Discounts" description="Applied to the bill on the server." />
      <CardBody className="space-y-5">
        {quote.promoCode ? (
          <p className="text-success text-sm">
            Promo code <span className="font-semibold">{quote.promoCode}</span> applied.
          </p>
        ) : (
          <div className="space-y-2">
            <label htmlFor="promo" className="text-sm font-medium">
              Promo code
            </label>
            <div className="flex gap-2">
              <Input
                id="promo"
                value={code}
                onChange={(e) => {
                  setCode(e.target.value);
                  setPreview(null);
                }}
                placeholder="e.g. SAVE50"
              />
              <Button
                variant="outline"
                onClick={() => check.mutate()}
                disabled={!code.trim()}
                loading={check.isPending}
              >
                Check
              </Button>
            </div>
            {preview ? (
              <div className="bg-success-soft flex flex-wrap items-center justify-between gap-2 rounded-lg px-3 py-2 text-sm">
                <span className="text-success">
                  You save {formatMoney(preview.discount)}. New total {formatMoney(preview.payable)}
                  .
                </span>
                <Button size="sm" loading={apply.isPending} onClick={() => apply.mutate()}>
                  Apply code
                </Button>
              </div>
            ) : null}
          </div>
        )}

        {quote.pointsRedeemed ? (
          <p className="text-success text-sm">{quote.pointsRedeemed} loyalty points redeemed.</p>
        ) : pointOptions.length ? (
          <div className="space-y-2">
            <label htmlFor="points" className="text-sm font-medium">
              Redeem loyalty points{" "}
              <span className="text-fg-muted font-normal">
                ({balance} available · 100 pts = ₹10)
              </span>
            </label>
            <div className="flex gap-2">
              <Select id="points" value={points} onChange={(e) => setPoints(e.target.value)}>
                <option value="">Choose points</option>
                {pointOptions.map((p) => (
                  <option key={p} value={p}>
                    {p} pts (−{formatMoney(p / 10)})
                  </option>
                ))}
              </Select>
              <Button
                variant="outline"
                disabled={!points}
                loading={redeem.isPending}
                onClick={() => redeem.mutate()}
              >
                Redeem
              </Button>
            </div>
          </div>
        ) : null}
      </CardBody>
    </Card>
  );
}

export function PayView({ requestId }: { requestId: number }) {
  const user = useSession();
  const router = useRouter();
  const queryClient = useQueryClient();
  const request = useQuery({
    queryKey: qk.customer.request(requestId),
    queryFn: () => customerApi.getRequest(requestId),
  });
  const billId = request.data?.bill?.id ?? 0;
  const billOpen = !!billId && !request.data?.bill?.isPaid;
  const quote = useQuery({
    queryKey: qk.customer.quote(billId),
    queryFn: () => customerApi.getQuote(billId),
    enabled: billOpen,
  });

  const [step, setStep] = useState<Step>("idle");
  const [error, setError] = useState<string | null>(null);
  // One key per payment attempt: retries reuse the same Razorpay order.
  const [orderKey, setOrderKey] = useState(newKey);

  const cash = useAction({
    fn: () => customerApi.chooseCash(billId),
    invalidate: [qk.customer.request(requestId)],
    success: (r) => r.message ?? `Pay ${formatMoney(r.payable)} in cash to your technician.`,
  });

  async function payOnline() {
    setError(null);
    setStep("creating");
    try {
      const order = await customerApi.createRazorpayOrder(billId, orderKey);
      setStep("checkout");
      const result = await openCheckout({
        key: order.keyId, // public key id from the API; never hard-coded
        amount: order.amount, // paise, already discounted by the server
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
      setOrderKey(newKey());
      await queryClient.invalidateQueries({ queryKey: qk.customer.all });
      toast.success("Payment successful! Your request is now complete.");
      router.replace(`/customer/requests/${requestId}`);
    } catch (e) {
      setStep("idle");
      setError(e instanceof PaymentCancelledError ? e.message : errorMessage(e));
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
        const choseCash = bill.preferredPaymentMethod === "Cash";
        return (
          <>
            <PageHeader
              back={{ href: `/customer/requests/${requestId}`, label: `Request #${requestId}` }}
              title="Pay your bill"
            />
            <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
              <div className="space-y-6">
                <Card>
                  <CardHeader title={`Bill #${bill.id}`} />
                  <CardBody>
                    <BillSummary bill={bill} />
                  </CardBody>
                </Card>
                {quote.data ? <Discounts billId={bill.id} quote={quote.data} /> : null}
              </div>

              <Card className="h-fit">
                <CardBody className="space-y-4">
                  {quote.data ? <QuoteLines quote={quote.data} /> : <Skeleton className="h-20" />}
                  <FormError
                    message={error ?? (quote.isError ? errorMessage(quote.error) : null)}
                  />
                  <Button
                    className="w-full"
                    size="lg"
                    loading={busy}
                    disabled={!quote.data}
                    onClick={() => void payOnline()}
                    icon={<Icon name="card" className="size-4" />}
                  >
                    {label ?? `Pay ${quote.data ? formatMoney(quote.data.payable) : ""} online`}
                  </Button>
                  <p className="text-fg-muted flex items-center justify-center gap-1.5 text-xs">
                    <Icon name="shield" className="size-3.5" /> Secured by Razorpay
                  </p>
                  <div className="border-border border-t pt-4">
                    {choseCash ? (
                      <p className="text-sm" role="status">
                        You chose to pay in cash. Hand the amount to your technician; they&apos;ll
                        confirm it and the job will complete.
                      </p>
                    ) : (
                      <Button
                        variant="outline"
                        className="w-full"
                        disabled={busy}
                        loading={cash.isPending}
                        onClick={() => cash.mutate()}
                      >
                        I&apos;ll pay in cash
                      </Button>
                    )}
                  </div>
                </CardBody>
              </Card>
            </div>
          </>
        );
      }}
    </QueryView>
  );
}
