import type { APIRoute } from 'astro';

export const prerender = false;

export const GET: APIRoute = () =>
  new Response(JSON.stringify({ now: new Date().toISOString() }), {
    headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' },
  });
