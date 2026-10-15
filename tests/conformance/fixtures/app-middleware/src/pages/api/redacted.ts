import type { APIRoute } from 'astro';

export const GET: APIRoute = () =>
  new Response(JSON.stringify({ name: 'secret', value: 42 }), {
    headers: { 'Content-Type': 'application/json' },
  });
