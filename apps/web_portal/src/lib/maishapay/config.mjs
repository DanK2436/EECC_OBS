import fs from "node:fs";
import path from "node:path";

export const SERVER_ENV_NAMES = [
  "MAISHAPAY_PUBLIC_KEY",
  "MAISHAPAY_SECRET_KEY",
  "MAISHAPAY_WEBHOOK_SECRET",
];

export const OFFICIAL_COLLECT_URL = "https://marchand.maishapay.online/api/payment/rest/vers1.0/merchant";
export const ALLOWED_AMOUNTS_USD = [5, 10, 20, 50, 100, 200];
export const MOBILE_PROVIDERS = ["AIRTEL", "MPESA", "ORANGE"];

export function parseEnvText(text) {
  const values = {};
  for (const line of String(text || "").split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const eq = trimmed.indexOf("=");
    if (eq < 1) continue;
    const key = trimmed.slice(0, eq).trim();
    let value = trimmed.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    values[key] = value;
  }
  return values;
}

export function classifyKey(value, kind) {
  const text = String(value || "");
  if (!text) return "absent";
  if (kind === "public" && text.startsWith("MP-SBPK-")) return "sandbox";
  if (kind === "secret" && text.startsWith("MP-SBSK-")) return "sandbox";
  if (kind === "public" && text.startsWith("MP-LIVEPK-")) return "live";
  if (kind === "secret" && text.startsWith("MP-LIVESK-")) return "live";
  return "unrecognized";
}

export function assessMaishaEnv(values) {
  const publicMode = classifyKey(values.MAISHAPAY_PUBLIC_KEY, "public");
  const secretMode = classifyKey(values.MAISHAPAY_SECRET_KEY, "secret");
  let blocked = null;
  if (publicMode === "absent" || secretMode === "absent") blocked = "variables-absentes";
  else if (publicMode === "unrecognized" || secretMode === "unrecognized") blocked = "format-non-reconnu";
  else if (publicMode !== secretMode) blocked = "modes-incoherents";
  else if (publicMode === "live") blocked = "mode-live-refuse";
  return {
    blocked,
    gatewayMode: blocked ? null : 0,
    publicMode,
    secretMode,
    webhookConfigured: Boolean(values.MAISHAPAY_WEBHOOK_SECRET),
    names: SERVER_ENV_NAMES,
  };
}

export function mergeServerEnv(layers) {
  const merged = {};
  for (const layer of layers) {
    for (const name of SERVER_ENV_NAMES) {
      if (layer && layer[name]) merged[name] = layer[name];
    }
  }
  return merged;
}

export function envFilePaths(portalDir) {
  const root = path.resolve(portalDir, "../..");
  return [
    path.join(root, ".env"),
    path.join(root, ".env.local"),
    path.join(portalDir, ".env"),
    path.join(portalDir, ".env.local"),
  ];
}

export function readServerMaishaEnv(portalDir, processEnv = process.env) {
  const files = [];
  const layers = [];
  for (const filePath of envFilePaths(portalDir)) {
    if (!fs.existsSync(filePath)) {
      files.push({ path: filePath, accessible: false });
      continue;
    }
    files.push({ path: filePath, accessible: true });
    layers.push(parseEnvText(fs.readFileSync(filePath, "utf8")));
  }
  layers.push(processEnv);
  const values = mergeServerEnv(layers);
  return { files, assessment: assessMaishaEnv(values), values };
}

export function publicConfigReport(loaded) {
  return {
    files: loaded.files.map((file) => ({ path: file.path, accessible: file.accessible })),
    ...loaded.assessment,
  };
}
