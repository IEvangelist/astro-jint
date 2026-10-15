import type { APIRoute } from 'astro';

export const GET: APIRoute = () =>
  new Response('{"error":"not found"}', {
    status: 404,
    headers: { 'Content-Type': 'application/json' },
  });
