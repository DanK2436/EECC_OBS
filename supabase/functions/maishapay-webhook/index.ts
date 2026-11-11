// ==============================================================================
// EECC Ecosystem - Edge Function : maishapay-webhook
// Réception et validation sécurisée des notifications de paiement (Dons)
// ==============================================================================

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.0";

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
    const webhookSecret = Deno.env.get("MAISHAPAY_WEBHOOK_SECRET");

    // Vérification du token ou de la signature si configurée
    const incomingSignature = req.headers.get("x-maishapay-signature");
    if (webhookSecret && incomingSignature && incomingSignature !== webhookSecret) {
      return new Response(JSON.stringify({ error: "Signature webhook invalide" }), {
        status: 401,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    const payload = await req.json();
    const { reference, transaction_id, status, amount, fee } = payload;

    if (!reference) {
      return new Response(JSON.stringify({ error: "Référence de transaction manquante" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

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

    // Normalisation du statut
    let targetStatus = "pending";
    if (status === "SUCCESS" || status === "COMPLETED" || status === "confirmed") {
      targetStatus = "confirmed";
    } else if (status === "FAILED" || status === "CANCELLED" || status === "declined") {
      targetStatus = "failed";
    }

    // Mise à jour de la table donations
    await supabaseClient
      .from("donations")
      .update({
        status: targetStatus,
        provider_transaction_id: transaction_id || donation.provider_transaction_id,
        provider_fee: fee !== undefined ? fee : donation.provider_fee,
      })
      .eq("id", donation.id);

    // Journalisation d'audit
    await supabaseClient.rpc("log_audit_event", {
      p_actor_id: donation.donor_id || null,
      p_action: targetStatus === "confirmed" ? "DONATION_CONFIRMED" : "DONATION_FAILED",
      p_entity_type: "donations",
      p_entity_id: donation.id,
      p_metadata: {
        reference: donation.reference,
        amount: donation.amount,
        currency: donation.currency,
        provider_transaction_id: transaction_id,
        status: targetStatus,
      },
    });

    return new Response(JSON.stringify({ received: true, status: targetStatus }), {
      headers: { ...corsHeaders, "Content-Type": "application/json" },
    });
  } catch (error) {
    return new Response(JSON.stringify({ error: (error as Error).message }), {
      status: 500,
      headers: { ...corsHeaders, "Content-Type": "application/json" },
    });
  }
});
