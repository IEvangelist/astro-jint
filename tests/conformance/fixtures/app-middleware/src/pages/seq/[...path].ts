import type { APIRoute } from 'astro';

// The response function of the sequence tests, which build it with createResponseFunction(body): the body the combined
// handler of the URL gets from next().
const bodies: Record<string, string> = {
  none: 'passthrough',
  single: 'single',
  upper: 'hello world',
  mixed: 'mixed',
  'short-circuit': 'should not reach',
};

export const GET: APIRoute = ({ params, locals }) => {
  const path = params.path ?? '';
  if (path === 'propagate') {
    const { first, second } = locals as Record<string, unknown>;
    return new Response(`${first}-${second}`);
  }
  return new Response(bodies[path] ?? 'OK');
};
