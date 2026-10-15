import type { APIRoute } from 'astro';
import { cases as classListAndStyle } from '../../lib/probes/class-list-and-style';
import { cases as htmlPrimitives } from '../../lib/probes/html-primitives';

export const prerender = false;

// The calls that the unit tests of the runtime helpers (astro/runtime/server) make, run inside the host. GET /probe/<name> runs
// the cases of the probe module <name> and answers with one entry per case: { value } or, when the call threw, { error }.
const probes: Record<string, Record<string, () => unknown>> = {
  'class-list-and-style': classListAndStyle,
  'html-primitives': htmlPrimitives,
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
