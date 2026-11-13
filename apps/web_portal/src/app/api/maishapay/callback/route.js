import path from "node:path";
import { readServerMaishaEnv } from "../../../../lib/maishapay/config.mjs";
import { interpretBrowserReturn, recallIntent } from "../../../../lib/maishapay/collect.mjs";
import { decidePaymentUpdate, signatureMatches } from "../../../../lib/maishapay/decision.mjs";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(request) {
  const url = new URL(request.url);
  return Response.json(interpretBrowserReturn({
    status: url.searchParams.get("status"),
    description: url.searchParams.get("description"),
  }));
}

export async function POST(request) {
  const loaded = readServerMaishaEnv(path.resolve(process.cwd()));
  const rawBody = await request.text();
  const signatureOk = await signatureMatches(
    loaded.values.MAISHAPAY_WEBHOOK_SECRET || "",
    rawBody,
    request.headers.get("x-maishapay-signature") || "",
  );
  let payload = {};
  try {
    payload = rawBody ? JSON.parse(rawBody) : {};
  } catch {
    payload = {};
  }
  const reference = typeof payload.transactionReference === "string"
    ? payload.transactionReference
    : typeof payload.reference === "string" ? payload.reference : "";
  const intent = recallIntent(reference);
  if (!intent) {
    return Response.json({
      paid: false,
      state: "intention-inconnue",
      message: "Aucune intention serveur ne correspond à cette référence. Le paiement n'est pas confirmé.",
    }, { status: 409 });
  }
  const decision = decidePaymentUpdate({
    secretConfigured: Boolean(loaded.values.MAISHAPAY_WEBHOOK_SECRET),
    signatureOk,
    reference,
    reportedStatus: payload.status,
    reportedAmount: typeof payload.amount === "number" ? payload.amount : null,
    storedAmount: intent.amount,
    reportedCurrency: payload.currency,
    storedCurrency: intent.currency,
    storedStatus: intent.status || "pending",
  });
  if (decision.update?.status === "confirmed") intent.status = "confirmed";
  const paid = decision.update?.status === "confirmed" || decision.body.idempotent === true;
  return Response.json({ ...decision.body, paid }, { status: decision.http });
}
