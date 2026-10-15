import type { APIRoute } from 'astro';

export const GET: APIRoute = () =>
  new Response(JSON.stringify({ name: 'test' }), {
    headers: { 'Content-Type': 'application/json' },
  });
