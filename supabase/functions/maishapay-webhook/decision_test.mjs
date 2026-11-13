import assert from "node:assert/strict";
import test from "node:test";
import { decidePaymentUpdate, signatureMatches } from "./decision.mjs";

const secret = "test-webhook-secret";
const body = JSON.stringify({ reference: "DON-1", amount: 20, currency: "USD", status: "COMPLETED" });

test("TEST_ONLY_PROVIDER_MOCK: unsigned or unconfigured callbacks do not confirm", async () => {
  assert.equal((await decidePaymentUpdate({ secretConfigured: false, signatureOk: true, reference: "DON-1", reportedStatus: "COMPLETED", reportedAmount: 20, storedAmount: 20 })).http, 503);
  assert.equal((await decidePaymentUpdate({ secretConfigured: true, signatureOk: false, reference: "DON-1", reportedStatus: "COMPLETED", reportedAmount: 20, storedAmount: 20 })).http, 401);
});

test("TEST_ONLY_PROVIDER_MOCK: amount mismatch and repeat confirmation", async () => {
  const mismatch = await decidePaymentUpdate({
    secretConfigured: true,
    signatureOk: true,
    reference: "DON-1",
    reportedStatus: "SUCCESS",
    reportedAmount: 5,
    storedAmount: 20,
    reportedCurrency: "USD",
    storedCurrency: "USD",
    storedStatus: "pending",
  });
  assert.equal(mismatch.http, 409);
  assert.equal(mismatch.update, null);

  const first = await decidePaymentUpdate({
    secretConfigured: true,
    signatureOk: true,
    reference: "DON-1",
    reportedStatus: "COMPLETED",
    reportedAmount: 20,
    storedAmount: 20,
    reportedCurrency: "USD",
    storedCurrency: "USD",
    storedStatus: "pending",
  });
  assert.equal(first.update.status, "confirmed");

  const again = await decidePaymentUpdate({
    secretConfigured: true,
    signatureOk: true,
    reference: "DON-1",
    reportedStatus: "COMPLETED",
    reportedAmount: 20,
    storedAmount: 20,
    reportedCurrency: "USD",
    storedCurrency: "USD",
    storedStatus: "confirmed",
  });
  assert.equal(again.body.idempotent, true);
  assert.equal(again.update, null);
});

test("TEST_ONLY_PROVIDER_MOCK: HMAC accepts only the body signed with the server secret", async () => {
  const header = await hmacHex(secret, body);
  assert.equal(await signatureMatches(secret, body, header), true);
  assert.equal(await signatureMatches(secret, body, header.slice(0, -1) + (header.endsWith("0") ? "1" : "0")), false);
  assert.equal(await signatureMatches("", body, header), false);
});

async function hmacHex(secretValue, rawBody) {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secretValue),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const signed = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(rawBody));
  return [...new Uint8Array(signed)].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}
