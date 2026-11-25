// ==============================================================================
// EECC Ecosystem - Edge Function : create-donation-intent
// Création sécurisée d'intention de don Maishapay (Section 8.1 du cahier des charges)
// ==============================================================================

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.0";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
};

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

    // Vérifier le rôle (fidele, editor, producer, admin — tous peuvent faire un don)
    const { data: userRole } = await supabaseClient
      .from("user_roles")
      .select("role")
      .eq("user_id", user.id)
      .single();

    // Récupérer les données du don
    const {
      amount,
      currency,
      donor_name,
      donor_email,
      donor_phone,
      method,
    } = await req.json();

    if (!amount || amount <= 0) {
      return new Response(JSON.stringify({ error: "Montant invalide" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    const reference = `DON-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;

    // Créer l'intention de don
    const { data: donation, error: dError } = await supabaseClient
      .from("donations")
      .insert({
        reference,
        donor_name: donor_name || null,
        donor_email: donor_email || null,
        donor_phone: donor_phone || null,
        amount: Number(amount),
        currency: currency || "USD",
        method: method || "mobile_money",
        status: "pending",
      })
      .select()
      .single();

    if (dError) throw dError;

    // Journalisation
    await supabaseClient.rpc("log_audit_event", {
      p_actor_id: user.id,
      p_action: "DONATION_INTENT_CREATED",
      p_entity_type: "donations",
      p_entity_id: donation.id,
      p_metadata: {
        reference: donation.reference,
        amount: donation.amount,
        currency: donation.currency,
        donor_name: donation.donor_name,
        status: donation.status,
      },
    });

    // Générer l'URL de paiement Maishapay
    // Note : la vraie URL de Maishapay utiliserait le backend. Ici on génère une URL
    // avec la référence pour que Maishapay puisse retrouver l'intention.
    const maishapayCheckoutUrl = `${Deno.env.get("MAISHAPAY_API_URL") || "https://api.maishapay.net/v1"}/donations/${reference}/checkout`;

    return new Response(
      JSON.stringify({
        success: true,
        donation: {
          id: donation.id,
          reference: donation.reference,
          amount: donation.amount,
          currency: donation.currency,
          status: donation.status,
        },
        checkout_url: maishapayCheckoutUrl,
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