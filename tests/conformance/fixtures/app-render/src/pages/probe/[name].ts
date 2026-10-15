import type { APIRoute } from 'astro';
import { cases as classListAndStyle } from '../../lib/probes/class-list-and-style';
import { cases as escape } from '../../lib/probes/escape';
import { cases as head } from '../../lib/probes/head';
import { cases as htmlPrimitives } from '../../lib/probes/html-primitives';
import { cases as hydration } from '../../lib/probes/hydration';
import { cases as inertScriptDedup } from '../../lib/probes/inert-script-dedup';
import { cases as jsxCustomElements } from '../../lib/probes/jsx-custom-elements';
import { cases as rendering } from '../../lib/probes/rendering';
import { cases as ssrElements } from '../../lib/probes/ssr-elements';
import { cases as tags } from '../../lib/probes/tags';
import { cases as transitions } from '../../lib/probes/transitions';

export const prerender = false;

// The calls that the unit tests of the runtime helpers (astro/runtime/server) make, run inside the host. GET /probe/<name> runs
// the cases of the probe module <name> and answers with one entry per case: { value } or, when the call threw, { error }.
const probes: Record<string, Record<string, () => unknown>> = {
  'class-list-and-style': classListAndStyle,
  escape,
  head,
  'html-primitives': htmlPrimitives,
  hydration,
  'inert-script-dedup': inertScriptDedup,
  'jsx-custom-elements': jsxCustomElements,
  rendering,
  'ssr-elements': ssrElements,
  tags,
  transitions,
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
