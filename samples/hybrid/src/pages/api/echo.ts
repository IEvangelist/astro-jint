import type { APIRoute } from 'astro';

export const prerender = false;

export const ALL: APIRoute = async ({ request, url, clientAddress, locals }) => {
  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
  const echo = {
    method: request.method,
    path: url.pathname,
    query: Object.fromEntries(url.searchParams),
    contentType: request.headers.get('content-type'),
    body: hasBody ? await request.text() : null,
    clientAddress,
    traceId: locals.traceId,
  };

  return new Response(JSON.stringify(echo), { headers: { 'Content-Type': 'application/json' } });
};
