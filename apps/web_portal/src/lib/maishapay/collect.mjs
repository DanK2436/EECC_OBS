import { ALLOWED_AMOUNTS_USD, MOBILE_PROVIDERS, OFFICIAL_COLLECT_URL } from "./config.mjs";

const intents = new Map();

export function rememberIntent(intent) {
  intents.set(intent.reference, intent);
}

export function recallIntent(reference) {
  return intents.get(reference) || null;
}

const BLOCKED_MESSAGE = {
  "variables-absentes": "Le serveur n'a pas chargé MAISHAPAY_PUBLIC_KEY et MAISHAPAY_SECRET_KEY. Aucun appel n'a été envoyé.",
  "format-non-reconnu": "Les variables serveur ne portent pas un préfixe sandbox ou live reconnu. Aucun appel n'a été envoyé.",
  "modes-incoherents": "La clé publique et la clé secrète ne sont pas du même mode. Aucun appel n'a été envoyé.",
  "mode-live-refuse": "Les variables serveur sont en mode live. Cette version refuse l'appel de production.",
};

export function resolveAmount(requested) {
  const amount = Number(requested);
  return ALLOWED_AMOUNTS_USD.includes(amount) ? amount : null;
}

export function buildCollectBody(values, payment) {
  return {
    gatewayMode: 0,
    publicApiKey: values.MAISHAPAY_PUBLIC_KEY,
    secretApiKey: values.MAISHAPAY_SECRET_KEY,
    transactionReference: payment.reference,
    amount: payment.amount,
    currency: "USD",
    customerFullName: payment.name || null,
    customerPhoneNumber: payment.wallet,
    customerEmailAddress: payment.email || null,
    chanel: "MOBILEMONEY",
    provider: payment.provider,
    walletID: payment.wallet,
  };
}

export function interpretCollectResponse(payload) {
  const data = payload && payload.original && payload.original.data ? payload.original.data : {};
  const statusCode = String(data.statusCode || "");
  if (statusCode === "202") {
    return {
      state: "accepted",
      paid: false,
      providerTransactionId: data.transactionId ? String(data.transactionId) : null,
    };
  }
  return { state: "rejected", paid: false, providerTransactionId: null };
}

export function interpretBrowserReturn(query) {
  const status = String(query.status || "");
  const description = String(query.description || "");
  if (status === "202" || /accepted/i.test(description)) {
    return {
      paid: false,
      state: "accepted",
      message: "Le retour navigateur indique Accepted (202). Ce n'est pas un paiement confirmé.",
    };
  }
  return {
    paid: false,
    state: "unverified",
    message: "Le retour navigateur n'est pas signé. Le paiement n'est pas confirmé.",
  };
}

export async function startMobileMoneyCollection({ loaded, payment, fetchImpl, recordPending }) {
  const assessment = loaded.assessment;
  if (assessment.blocked) {
    return {
      http: assessment.blocked === "variables-absentes" ? 503 : 409,
      body: { paid: false, state: assessment.blocked, message: BLOCKED_MESSAGE[assessment.blocked] },
    };
  }
  const amount = resolveAmount(payment.amount);
  if (amount == null) {
    return { http: 400, body: { paid: false, state: "montant-refuse", message: "Le serveur n'accepte que les montants affichés." } };
  }
  if (!MOBILE_PROVIDERS.includes(payment.provider)) {
    return {
      http: 400,
      body: { paid: false, state: "moyen-non-couvert", message: "La carte bancaire n'est pas couverte par l'API REST Mobile Money. Aucun appel n'a été envoyé." },
    };
  }
  if (!/^\+243\d{9}$/.test(String(payment.wallet || ""))) {
    return { http: 400, body: { paid: false, state: "numero-invalide", message: "Le numéro doit être au format +243 suivi de 9 chiffres." } };
  }
  const reference = `EECC-${crypto.randomUUID()}`;
  rememberIntent({ reference, amount, currency: "USD", provider: payment.provider });
  const providerBody = buildCollectBody(loaded.values, { ...payment, amount, reference });
  if (recordPending) await recordPending({ reference, amount, currency: "USD", provider: payment.provider, wallet: payment.wallet });
  let payload;
  try {
    const response = await fetchImpl(OFFICIAL_COLLECT_URL, {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(providerBody),
    });
    payload = await response.json();
  } catch {
    return { http: 502, body: { paid: false, state: "provider-injoignable", reference, message: "MaishaPay n'a pas répondu. Aucun paiement n'est confirmé." } };
  }
  const interpreted = interpretCollectResponse(payload);
  return {
    http: interpreted.state === "accepted" ? 202 : 502,
    body: {
      paid: false,
      state: interpreted.state,
      reference,
      providerTransactionId: interpreted.providerTransactionId,
      message: interpreted.state === "accepted"
        ? "MaishaPay a répondu Accepted (202). Le téléphone doit encore confirmer. Ce n'est pas un paiement confirmé."
        : "MaishaPay a refusé la demande. Aucun paiement n'est confirmé.",
    },
  };
}
