import type { APIRoute } from 'astro';

// A route that has a POST handler only: a GET request to it is answered with the 404 page.
export const POST: APIRoute = () => new Response('ok');
