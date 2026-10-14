import type { APIRoute } from 'astro';

export const GET: APIRoute = ({ locals }) =>
  new Response(JSON.stringify({ ...locals }), { headers: { 'Content-Type': 'application/json' } });
