import path from "node:path";
import { publicConfigReport, readServerMaishaEnv } from "../../../lib/maishapay/config.mjs";
import { startMobileMoneyCollection } from "../../../lib/maishapay/collect.mjs";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function portalDir() {
  return path.resolve(process.cwd());
}

export async function GET() {
  const loaded = readServerMaishaEnv(portalDir());
  return Response.json({ paid: false, ...publicConfigReport(loaded) });
}

export async function POST(request) {
  const loaded = readServerMaishaEnv(portalDir());
  let payment = {};
  try {
    payment = await request.json();
  } catch {
    payment = {};
  }
  const result = await startMobileMoneyCollection({
    loaded,
    payment,
    fetchImpl: fetch,
  });
  return Response.json(result.body, { status: result.http });
}
