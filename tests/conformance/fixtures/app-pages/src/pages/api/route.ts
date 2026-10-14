import type { APIRoute } from 'astro';

// Only POST is defined: any other method has no handler, which Astro answers with the 404 page.
export const POST: APIRoute = () => new Response(JSON.stringify({ ok: true }));
