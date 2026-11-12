// ==============================================================================
// EECC Ecosystem - Edge Function : withdrawal-verify-otp
// Vérification sécurisée du code OTP et déclenchement du décaissement Maishapay
// Conforme à la Section 20.3 & 20.5 du cahier des charges
// ==============================================================================

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.0";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
};

async function hashOtpWithSalt(otp: string, salt: string): Promise<string> {
  const encoder = new TextEncoder();
  const data = encoder.encode(otp.toUpperCase().trim() + salt);
  const hashBuffer = await crypto.subtle.digest("SHA-256", data);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map((b) => b.toString(16).padStart(2, "0")).join("");
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

    const { withdrawal_id, otp_code } = await req.json();

    if (!withdrawal_id || !otp_code || typeof otp_code !== "string" || otp_code.trim().length !== 5) {
      return new Response(JSON.stringify({ error: "Le code de vérification doit comporter exactement 5 caractères." }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Récupérer la demande
    const { data: withdrawal, error: wError } = await supabaseClient
      .from("withdrawal_requests")
      .select("*")
      .eq("id", withdrawal_id)
      .single();

    if (wError || !withdrawal) {
      return new Response(JSON.stringify({ error: "Demande de retrait introuvable" }), {
        status: 404,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Vérifications d'état
    if (withdrawal.status !== "pending_otp") {
      return new Response(JSON.stringify({ error: `La demande n'est pas en attente de code (statut actuel: ${withdrawal.status})` }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Vérification de l'expiration
    if (new Date(withdrawal.otp_expires_at) < new Date()) {
      await supabaseClient
        .from("withdrawal_requests")
        .update({ status: "otp_expired" })
        .eq("id", withdrawal_id);

      return new Response(JSON.stringify({ error: "Le code de vérification a expiré. Veuillez demander un nouveau code." }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Vérification du nombre maximal d'essais (max 5)
    if (withdrawal.otp_attempts >= 5) {
      await supabaseClient
        .from("withdrawal_requests")
        .update({ status: "locked" })
        .eq("id", withdrawal_id);

      return new Response(JSON.stringify({ error: "Nombre maximal d'essais atteint. Cette demande a été verrouillée pour des raisons de sécurité." }), {
        status: 403,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Comparaison du hachage
    const computedHash = await hashOtpWithSalt(otp_code, withdrawal.otp_salt);
    if (computedHash !== withdrawal.otp_hash) {
      const nextAttempts = withdrawal.otp_attempts + 1;
      const willLock = nextAttempts >= 5;

      await supabaseClient
        .from("withdrawal_requests")
        .update({
          otp_attempts: nextAttempts,
          status: willLock ? "locked" : "pending_otp",
        })
        .eq("id", withdrawal_id);

      await supabaseClient.rpc("log_audit_event", {
        p_actor_id: user.id,
        p_action: "OTP_FAILED_ATTEMPT",
        p_entity_type: "withdrawal_requests",
        p_entity_id: withdrawal_id,
        p_metadata: { attempts: nextAttempts, locked: willLock },
      });

      return new Response(
        JSON.stringify({
          error: "Code de vérification incorrect.",
          remaining_attempts: Math.max(0, 5 - nextAttempts),
        }),
        {
          status: 400,
          headers: { ...corsHeaders, "Content-Type": "application/json" },
        }
      );
    }

    // Succès de la vérification OTP
    await supabaseClient
      .from("withdrawal_requests")
      .update({
        status: "otp_verified",
        otp_verified_at: new Date().toISOString(),
      })
      .eq("id", withdrawal_id);

    await supabaseClient.rpc("log_audit_event", {
      p_actor_id: user.id,
      p_action: "OTP_VERIFIED_SUCCESS",
      p_entity_type: "withdrawal_requests",
      p_entity_id: withdrawal_id,
      p_metadata: { reference: withdrawal.reference },
    });

    const failureMsg = "Décaissement non envoyé. Cette version ne contacte pas MaishaPay et n'utilise pas de clé marchand.";
    await supabaseClient
      .from("withdrawal_requests")
      .update({
        status: "payout_not_sent",
        failure_reason: failureMsg,
      })
      .eq("id", withdrawal_id);

    await supabaseClient.rpc("log_audit_event", {
      p_actor_id: user.id,
      p_action: "WITHDRAWAL_PAYOUT_NOT_SENT",
      p_entity_type: "withdrawal_requests",
      p_entity_id: withdrawal_id,
      p_metadata: {
        reference: withdrawal.reference,
        amount_net: withdrawal.amount_net,
        currency: withdrawal.currency,
        status: "payout_not_sent",
      },
    });

    return new Response(
      JSON.stringify({
        success: false,
        status: "payout_not_sent",
        error: failureMsg,
      }),
      {
        status: 409,
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
