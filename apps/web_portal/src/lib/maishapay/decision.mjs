export function normalizeProviderStatus(status) {
  const value = String(status ?? "").toUpperCase();
  if (value === "SUCCESS" || value === "COMPLETED" || value === "CONFIRMED") return "confirmed";
  if (value === "FAILED" || value === "CANCELLED" || value === "DECLINED") return "failed";
  return "pending";
}

export async function signatureMatches(secret, rawBody, header) {
  if (!secret || !header) return false;
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const signed = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(rawBody));
  const expected = [...new Uint8Array(signed)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
  const presented = String(header).trim().toLowerCase().replace(/^sha256=/, "");
  if (expected.length !== presented.length) return false;
  let difference = 0;
  for (let index = 0; index < expected.length; index += 1) {
    difference |= expected.charCodeAt(index) ^ presented.charCodeAt(index);
  }
  return difference === 0;
}

export function decidePaymentUpdate(input) {
  if (!input.secretConfigured) {
    return { http: 503, body: { error: "callback-non-configuré" }, update: null };
  }
  if (!input.signatureOk) {
    return { http: 401, body: { error: "signature-invalide" }, update: null };
  }
  if (!input.reference) {
    return { http: 400, body: { error: "référence-absente" }, update: null };
  }
  const status = normalizeProviderStatus(input.reportedStatus);
  if (status === "confirmed") {
    if (input.reportedAmount == null || Number(input.reportedAmount) !== Number(input.storedAmount)) {
      return { http: 409, body: { error: "montant-différent" }, update: null };
    }
    if (
      input.reportedCurrency &&
      input.storedCurrency &&
      String(input.reportedCurrency).toUpperCase() !== String(input.storedCurrency).toUpperCase()
    ) {
      return { http: 409, body: { error: "devise-différente" }, update: null };
    }
    if (input.storedStatus === "confirmed") {
      return { http: 200, body: { received: true, status: "confirmed", idempotent: true }, update: null };
    }
  }
  return { http: 200, body: { received: true, status }, update: { status } };
}
