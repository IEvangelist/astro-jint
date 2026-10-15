import type { APIRoute } from 'astro';

// The endpoint twin of match-info.astro: answers with the pattern of the route, its params, the names of their keys and the
// pathname the route saw, as JSON.
export const GET: APIRoute = ({ params, routePattern, url }) =>
  new Response(JSON.stringify({ pattern: routePattern, params, keys: Object.keys(params), pathname: url.pathname }), {
    headers: { 'Content-Type': 'application/json' },
  });
