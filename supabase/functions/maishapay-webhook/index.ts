// ==============================================================================
// EECC Ecosystem - Edge Function : maishapay-webhook
// Réception et validation sécurisée des notifications de paiement (Dons)
// ==============================================================================

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.0";
import { decidePaymentUpdate, signatureMatches } from "./decision.mjs";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type, x-maishapay-signature",
};

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const webhookSecret = Deno.env.get("MAISHAPAY_WEBHOOK_SECRET") ?? "";
    const incomingSignature = req.headers.get("x-maishapay-signature");
    const rawBody = await req.text();
    const signatureOk = await signatureMatches(webhookSecret, rawBody, incomingSignature ?? "");
    let payload: Record<string, unknown> = {};
    try {
      payload = rawBody ? JSON.parse(rawBody) : {};
    } catch {
      payload = {};
    }
    const reference = typeof payload.reference === "string" ? payload.reference : "";
    const preliminary = decidePaymentUpdate({
      secretConfigured: webhookSecret.length > 0,
      signatureOk,
      reference,
      reportedStatus: "PENDING",
      reportedAmount: null,
      storedAmount: null,
      storedStatus: "pending",
    });
    if (!webhookSecret || !signatureOk || !reference) {
      return new Response(JSON.stringify(preliminary.body), {
        status: preliminary.http,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }
    const transactionId = typeof payload.transaction_id === "string" ? payload.transaction_id : null;
    const reportedStatus = payload.status ?? payload.payment_status;
    const reportedAmount = typeof payload.amount === "number" ? payload.amount : null;
    const reportedCurrency = typeof payload.currency === "string" ? payload.currency : null;
    const fee = typeof payload.fee === "number" ? payload.fee : undefined;

    const supabaseClient = createClient(supabaseUrl, supabaseServiceKey);

    // Récupérer le don correspondant
    const { data: donation, error: dError } = await supabaseClient
      .from("donations")
      .select("*")
      .eq("reference", reference)
      .single();

    if (dError || !donation) {
      return new Response(JSON.stringify({ error: "Don introuvable" }), {
        status: 404,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    const decision = decidePaymentUpdate({
      secretConfigured: true,
      signatureOk: true,
      reference,
      reportedStatus,
      reportedAmount,
      storedAmount: donation.amount,
      reportedCurrency,
      storedCurrency: donation.currency,
      storedStatus: donation.status,
    });

    if (decision.update) {
      await supabaseClient
        .from("donations")
        .update({
          status: decision.update.status,
          provider_transaction_id: transactionId || donation.provider_transaction_id,
          provider_fee: fee !== undefined ? fee : donation.provider_fee,
        })
        .eq("id", donation.id);

      await supabaseClient.rpc("log_audit_event", {
        p_actor_id: donation.donor_id || null,
        p_action: decision.update.status === "confirmed" ? "DONATION_CONFIRMED" : "DONATION_STATUS",
        p_entity_type: "donations",
        p_entity_id: donation.id,
        p_metadata: {
          reference: donation.reference,
          amount: donation.amount,
          currency: donation.currency,
          status: decision.update.status,
        },
      });
    }

    return new Response(JSON.stringify(decision.body), {
      status: decision.http,
      headers: { ...corsHeaders, "Content-Type": "application/json" },
    });
  } catch (error) {
    return new Response(JSON.stringify({ error: (error as Error).message }), {
      status: 500,
      headers: { ...corsHeaders, "Content-Type": "application/json" },
    });
  }
});
