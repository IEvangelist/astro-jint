import type { APIRoute } from 'astro';

export const ALL: APIRoute = () => new Response('oops', { status: 500, headers: { 'X-Debug': '1234' } });
