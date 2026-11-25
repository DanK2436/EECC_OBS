// ==============================================================================
// EECC Ecosystem - Edge Function : create-withdrawal-request
// Création d'une demande de retrait de trésorerie (brouillon)
// Conforme à la Section 20.3 du cahier des charges
// ==============================================================================

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.0";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
};

function generateReference(): string {
  const now = new Date();
  const date = now.toISOString().slice(0, 10).replace(/-/g, "");
  const random = Math.floor(Math.random() * 10000)
    .toString()
    .padStart(4, "0");
  return `WDR-${date}-${random}`;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;

    const authHeader = req.headers.get("Authorization");
    if (!authHeader) {
      return new Response(JSON.stringify({ error: "Non autorisé" }), {
        status: 401,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    const supabaseClient = createClient(supabaseUrl, supabaseServiceKey);
    const token = authHeader.replace("Bearer ", "");
    const { data: { user }, error: userError } = await supabaseClient.auth.getUser(token);

    if (userError || !user) {
      return new Response(JSON.stringify({ error: "Session invalide" }), {
        status: 401,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Vérifier les permissions du rôle (Trésorier ou Admin)
    const { data: userRole } = await supabaseClient
      .from("user_roles")
      .select("role")
      .eq("user_id", user.id)
      .in("role", ["treasurer", "admin"])
      .single();

    if (!userRole) {
      return new Response(JSON.stringify({ error: "Permissions insuffisantes (Rôle Trésorier requis)" }), {
        status: 403,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    const {
      amount_gross,
      fee,
      amount_net,
      currency,
      destination_account,
      destination_provider,
    } = await req.json();

    if (!amount_gross || amount_gross <= 0) {
      return new Response(JSON.stringify({ error: "Montant brut invalide" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    if (!amount_net || amount_net <= 0) {
      return new Response(JSON.stringify({ error: "Montant net invalide" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    if (!destination_account || !destination_provider) {
      return new Response(JSON.stringify({ error: "Destination et fournisseur requis" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    const reference = generateReference();

    const { data: withdrawal, error: wError } = await supabaseClient
      .from("withdrawal_requests")
      .insert({
        reference,
        requested_by: user.id,
        amount_gross,
        fee: fee || 0,
        amount_net,
        currency: currency || "USD",
        destination_account,
        destination_provider,
        status: "draft",
      })
      .select()
      .single();

    if (wError) throw wError;

    // Journaliser la création de la demande
    await supabaseClient.rpc("log_audit_event", {
      p_actor_id: user.id,
      p_action: "WITHDRAWAL_INITIATED",
      p_entity_type: "withdrawal_requests",
      p_entity_id: withdrawal.id,
      p_metadata: {
        reference: withdrawal.reference,
        amount_gross,
        amount_net,
        currency,
        destination_provider,
      },
    });

    return new Response(
      JSON.stringify({
        success: true,
        withdrawal: {
          id: withdrawal.id,
          reference: withdrawal.reference,
          status: withdrawal.status,
          amount_gross,
          amount_net,
          currency,
        },
      }),
      {
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      }
    );
  } catch (error) {
    return new Response(JSON.stringify({ error: (error as Error).message }), {
      status: 500,
      headers: { ...corsHeaders, "Content-Type": "application/json" },
    });
  }
});
