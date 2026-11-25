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

async function verifyHmacSignature(secret: string, payload: string, signature: string): Promise<boolean> {
  try {
    const key = await crypto.subtle.importKey(
      "raw",
      new TextEncoder().encode(secret),
      { name: "HMAC", hash: "SHA-256" },
      false,
      ["verify"]
    );
    const sigBuffer = Uint8Array.from(atob(signature), (c) => c.charCodeAt(0));
    return await crypto.subtle.verify("HMAC", key, sigBuffer, new TextEncoder().encode(payload));
  } catch {
    return false;
  }
}

// Comparaison à temps constant pour éviter les timing attacks.
function constantTimeEq(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) {
    diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  }
  return diff === 0;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const webhookSecret = Deno.env.get("MAISHAPAY_WEBHOOK_SECRET");

    if (!webhookSecret) {
      return new Response(JSON.stringify({ error: "Webhook non configuré côté serveur" }), {
        status: 500,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    const rawBody = await req.text();
    const incomingSignature = req.headers.get("x-maishapay-signature");

    if (!incomingSignature) {
      return new Response(JSON.stringify({ error: "Signature webhook manquante" }), {
        status: 401,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    let signatureValid = false;
    // Tentative HMAC-SHA256 (signature cryptographique Maishapay)
    if (incomingSignature.startsWith("sha256=")) {
      signatureValid = await verifyHmacSignature(webhookSecret, rawBody, incomingSignature.slice(7));
    } else {
      // Fallback : token simple partagé (comparaison à temps constant)
      signatureValid = constantTimeEq(webhookSecret, incomingSignature);
    }

    if (!signatureValid) {
      return new Response(JSON.stringify({ error: "Signature webhook invalide" }), {
        status: 401,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    let payload: { reference?: string; transaction_id?: string; status?: string; amount?: number; fee?: number };
    try {
      payload = JSON.parse(rawBody);
    } catch {
      return new Response(JSON.stringify({ error: "Corps de requête invalide" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

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

    // Idempotence : si le don a déjà le statut visé, ne rien réécrire
    if (donation.status === targetStatus) {
      return new Response(
        JSON.stringify({ received: true, status: targetStatus, idempotent: true }),
        {
          status: 200,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
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
        idempotent: false,
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
