import type { APIRoute } from 'astro';
import { cases as encryption } from '../../lib/probes/encryption';
import { cases as endpoint } from '../../lib/probes/endpoint';
import { cases as serverIslands } from '../../lib/probes/server-islands';

export const prerender = false;

// The calls that the server island unit tests of Astro make on the helpers of the runtime (encryption, getRequestData, the
// component of a server island), run inside the host. GET /probe/<name> runs the cases of the probe module <name> and answers with
// one entry per case: { value } or, when the call threw, { error }.
const probes: Record<string, Record<string, () => unknown>> = {
  encryption,
  endpoint,
  'server-islands': serverIslands,
};

export const GET: APIRoute = async ({ params }) => {
  const cases = probes[params.name ?? ''];
  if (!cases) {
    return new Response('No such probe', { status: 404 });
  }

  const results: Record<string, { value: unknown } | { error: string }> = {};
  for (const [name, run] of Object.entries(cases)) {
    try {
      results[name] = { value: (await run()) ?? null };
    } catch (error) {
      results[name] = { error: String((error as Error)?.stack ?? error) };
    }
  }

  return new Response(JSON.stringify(results), { headers: { 'content-type': 'application/json' } });
};
