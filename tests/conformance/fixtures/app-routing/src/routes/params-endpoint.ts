import type { APIRoute } from 'astro';

// Answers with the params of the route as JSON.
export const GET: APIRoute = ({ params }) =>
  new Response(JSON.stringify(params), { headers: { 'Content-Type': 'application/json' } });
