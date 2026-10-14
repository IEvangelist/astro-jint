import type { APIRoute } from 'astro';

// A typical BFF or API proxy behind middleware authorization: echoes what the router and the URL made of the request.
export const GET: APIRoute = ({ params, url }) =>
  new Response(JSON.stringify({ path: params.path, url_pathname: url.pathname }), {
    headers: { 'Content-Type': 'application/json' },
  });
