// ==============================================================================
// EECC Ecosystem - Edge Function : brevo-send-otp
// Génération et envoi sécurisé d'OTP à 5 caractères via Brevo
// Conforme à la Section 20 du cahier des charges
// ==============================================================================

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.0";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
};

// Générateur cryptographique d'OTP de 5 caractères non ambigus (exclut O, 0, I, 1)
function generateSecure5CharOtp(): string {
  const charset = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
  const randomBytes = new Uint8Array(5);
  crypto.getRandomValues(randomBytes);
  let result = "";
  for (let i = 0; i < 5; i++) {
    result += charset[randomBytes[i] % charset.length];
  }
  return result;
}

// Fonction de hachage SHA-256 avec sel
async function hashOtpWithSalt(otp: string, salt: string): Promise<string> {
  const encoder = new TextEncoder();
  const data = encoder.encode(otp + salt);
  const hashBuffer = await crypto.subtle.digest("SHA-256", data);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map((b) => b.toString(16).padStart(2, "0")).join("");
}

// Masquage de l'adresse e-mail (ex: ad***@exemple.com)
function maskEmail(email: string): string {
  const [user, domain] = email.split("@");
  if (!user || !domain) return "***@***";
  const visible = user.slice(0, 2);
  return `${visible}***@${domain}`;
}

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const supabaseUrl = Deno.env.get("SUPABASE_URL")!;
    const supabaseServiceKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
    const brevoApiKey = Deno.env.get("BREVO_API_KEY");
    const senderEmail = Deno.env.get("BREVO_SENDER_EMAIL") || "kandedan28@gmail.com";
    const senderName = Deno.env.get("BREVO_SENDER_NAME") || "Sécurité Trésorerie EECC";

    if (!brevoApiKey) {
      throw new Error("BREVO_API_KEY non configurée sur le serveur.");
    }

    const authHeader = req.headers.get("Authorization");
    if (!authHeader) {
      return new Response(JSON.stringify({ error: "Non autorisé" }), {
        status: 401,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Client Supabase avec contexte utilisateur
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

    // Récupérer les paramètres de la demande
    const { withdrawal_id } = await req.json();
    if (!withdrawal_id) {
      return new Response(JSON.stringify({ error: "withdrawal_id requis" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Récupérer la demande de retrait
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

    // Récupérer l'adresse e-mail vérifiée de sécurité
    const { data: settings } = await supabaseClient
      .from("treasury_settings")
      .select("verified_security_email, otp_validity_minutes")
      .eq("id", 1)
      .single();

    const targetEmail = settings?.verified_security_email;
    const validityMinutes = settings?.otp_validity_minutes || 5;

    if (!targetEmail) {
      return new Response(JSON.stringify({ error: "Aucune adresse e-mail de sécurité configurée" }), {
        status: 400,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Générer l'OTP et le sel
    const otp = generateSecure5CharOtp();
    const salt = crypto.randomUUID();
    const otpHash = await hashOtpWithSalt(otp, salt);
    const expiresAt = new Date(Date.now() + validityMinutes * 60 * 1000).toISOString();

    // Mettre à jour la demande de retrait
    await supabaseClient
      .from("withdrawal_requests")
      .update({
        status: "pending_otp",
        otp_hash: otpHash,
        otp_salt: salt,
        otp_attempts: 0,
        otp_expires_at: expiresAt,
      })
      .eq("id", withdrawal_id);

    // Envoyer l'e-mail via Brevo
    const emailPayload = {
      sender: { name: senderName, email: senderEmail },
      to: [{ email: targetEmail }],
      subject: `[EECC Trésorerie] Code de confirmation de retrait : ${otp}`,
      htmlContent: `
        <div style="font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;">
          <h2 style="color: #1a365d;">Confirmation de retrait de trésorerie</h2>
          <p>Une demande de retrait de fonds a été initiée depuis EECC Studio.</p>
          <div style="background-color: #f7fafc; padding: 15px; border-radius: 6px; margin: 15px 0;">
            <p><strong>Référence :</strong> ${withdrawal.reference}</p>
            <p><strong>Montant brut :</strong> ${withdrawal.amount_gross} ${withdrawal.currency}</p>
            <p><strong>Montant net estimé :</strong> ${withdrawal.amount_net} ${withdrawal.currency}</p>
            <p><strong>Bénéficiaire :</strong> ${withdrawal.destination_provider} (${withdrawal.destination_account})</p>
          </div>
          <div style="text-align: center; margin: 30px 0;">
            <p style="font-size: 14px; color: #718096; margin-bottom: 5px;">Votre code de vérification (5 caractères) :</p>
            <div style="font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #2b6cb0; background: #edf2f7; padding: 12px; border-radius: 6px; display: inline-block;">
              ${otp}
            </div>
            <p style="font-size: 13px; color: #e53e3e; margin-top: 8px;">Ce code expire dans ${validityMinutes} minutes.</p>
          </div>
          <p style="font-size: 12px; color: #a0aec0; border-top: 1px solid #e2e8f0; padding-top: 10px;">
            Si vous n’êtes pas à l’origine de cette demande, contactez immédiatement l’administrateur de l’église.
          </p>
        </div>
      `,
    };

    const brevoRes = await fetch("https://api.brevo.com/v3/smtp/email", {
      method: "POST",
      headers: {
        "api-key": brevoApiKey,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(emailPayload),
    });

    if (!brevoRes.ok) {
      const errText = await brevoRes.text();
      console.error("Erreur API Brevo:", errText);
      return new Response(JSON.stringify({ error: "Échec d'envoi de l'e-mail de confirmation" }), {
        status: 502,
        headers: { ...corsHeaders, "Content-Type": "application/json" },
      });
    }

    // Journaliser l'envoi dans les logs d'audit (sans JAMAIS consigner le code OTP)
    await supabaseClient.rpc("log_audit_event", {
      p_actor_id: user.id,
      p_action: "OTP_SENT",
      p_entity_type: "withdrawal_requests",
      p_entity_id: withdrawal_id,
      p_metadata: {
        reference: withdrawal.reference,
        masked_email: maskEmail(targetEmail),
        expires_at: expiresAt,
      },
    });

    return new Response(
      JSON.stringify({
        success: true,
        masked_email: maskEmail(targetEmail),
        expires_in_minutes: validityMinutes,
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
