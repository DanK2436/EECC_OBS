import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";
import { assessMaishaEnv, mergeServerEnv, parseEnvText, publicConfigReport } from "./config.mjs";
import { interpretBrowserReturn, interpretCollectResponse, startMobileMoneyCollection } from "./collect.mjs";

const SANDBOX_FILE = [
  "MAISHAPAY_PUBLIC_KEY=MP-SBPK-unit-test",
  "MAISHAPAY_SECRET_KEY=MP-SBSK-unit-test",
  "MAISHAPAY_WEBHOOK_SECRET=unit-test-secret",
].join("\n");

test("callback decision stays the same in the portal and the edge function", () => {
  const portal = fs.readFileSync(new URL("./decision.mjs", import.meta.url), "utf8");
  const edge = fs.readFileSync(new URL("../../../../../supabase/functions/maishapay-webhook/decision.mjs", import.meta.url), "utf8");
  assert.equal(portal, edge);
});

test("TEST_ONLY_PROVIDER_MOCK: server env reports mode without exposing values", () => {
  const values = mergeServerEnv([parseEnvText(SANDBOX_FILE), { MAISHAPAY_PUBLIC_KEY: "MP-SBPK-from-process" }]);
  const assessment = assessMaishaEnv(values);
  assert.equal(assessment.gatewayMode, 0);
  assert.equal(assessment.blocked, null);
  assert.equal(assessment.webhookConfigured, true);
  const report = publicConfigReport({ files: [{ path: "/repo/.env", accessible: false }], assessment });
  const serialized = JSON.stringify(report);
  assert.equal(serialized.includes("MP-SBPK"), false);
  assert.equal(serialized.includes("MP-SBSK"), false);
  assert.equal(serialized.includes("unit-test-secret"), false);
});

test("TEST_ONLY_PROVIDER_MOCK: live keys and missing keys never call the provider", async () => {
  let calls = 0;
  const fetchImpl = async () => {
    calls += 1;
    return { json: async () => ({}) };
  };
  const live = await startMobileMoneyCollection({
    loaded: {
      assessment: assessMaishaEnv({ MAISHAPAY_PUBLIC_KEY: "MP-LIVEPK-unit", MAISHAPAY_SECRET_KEY: "MP-LIVESK-unit" }),
      values: {},
    },
    payment: { amount: 20, provider: "AIRTEL", wallet: "+243810000001" },
    fetchImpl,
  });
  assert.equal(live.body.state, "mode-live-refuse");
  assert.equal(live.body.paid, false);
  const missing = await startMobileMoneyCollection({
    loaded: { assessment: assessMaishaEnv({}), values: {} },
    payment: { amount: 20, provider: "AIRTEL", wallet: "+243810000001" },
    fetchImpl,
  });
  assert.equal(missing.http, 503);
  assert.equal(calls, 0);
});

test("TEST_ONLY_PROVIDER_MOCK: sandbox 202 is accepted and not a confirmed payment", async () => {
  const result = await startMobileMoneyCollection({
    loaded: {
      assessment: assessMaishaEnv(parseEnvText(SANDBOX_FILE)),
      values: parseEnvText(SANDBOX_FILE),
    },
    payment: { amount: 20, provider: "MPESA", wallet: "+243810000001", name: "Test" },
    fetchImpl: async (_url, init) => {
      const sent = JSON.parse(init.body);
      assert.equal(sent.gatewayMode, 0);
      assert.equal(sent.amount, 20);
      assert.equal(sent.chanel, "MOBILEMONEY");
      return { json: async () => ({ original: { data: { statusCode: "202", statusDescription: "Accepted", transactionId: "109842" } } }) };
    },
  });
  assert.equal(result.http, 202);
  assert.equal(result.body.paid, false);
  assert.equal(result.body.providerTransactionId, "109842");
  assert.equal(JSON.stringify(result.body).includes("MP-SBSK"), false);
  assert.deepEqual(interpretCollectResponse({ original: { data: { statusCode: "200" } } }).paid, false);
  assert.equal(interpretBrowserReturn({ status: "202", description: "Accepted" }).paid, false);
});
