import { NextResponse } from 'next/server';

interface ReplayCheckResult {
  url: string;
  status: 'valid' | 'invalid' | 'unreachable';
  statusCode?: number;
  checkedAt: string;
}

export async function GET() {
  const replayUrls = [
    'https://www.youtube.com/watch?v=live_stream',
    'https://www.facebook.com/eecc.officiel/live',
  ];

  const results: ReplayCheckResult[] = [];

  for (const url of replayUrls) {
    try {
      const res = await fetch(url, { method: 'HEAD', signal: AbortSignal.timeout(3000) });
      results.push({
        url,
        status: res.ok ? 'valid' : 'invalid',
        statusCode: res.status,
        checkedAt: new Date().toISOString(),
      });
    } catch {
      results.push({
        url,
        status: 'unreachable',
        checkedAt: new Date().toISOString(),
      });
    }
  }

  return NextResponse.json({
    timestamp: new Date().toISOString(),
    totalChecked: results.length,
    results,
  });
}
