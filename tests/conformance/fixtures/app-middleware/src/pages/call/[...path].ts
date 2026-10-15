import type { APIRoute } from 'astro';

// The response function of the callMiddleware tests, which build it with createResponseFunction(body): the body the
// middleware of the URL gets from next().
const bodies: Record<string, string> = {
  replace: 'original',
  'next-undefined': 'from page',
  headers: 'OK',
  clone: '<p>testing</p>',
  'sync-next': 'sync OK',
};

export const GET: APIRoute = ({ params, locals }) => {
  const path = params.path ?? '';
  if (path === 'locals') {
    return new Response(`name=${(locals as Record<string, unknown>).name}`);
  }
  if (path === 'json') {
    return new Response(JSON.stringify({ name: 'secret', value: 42 }), {
      headers: { 'Content-Type': 'application/json' },
    });
  }
  return new Response(bodies[path] ?? 'OK');
};
