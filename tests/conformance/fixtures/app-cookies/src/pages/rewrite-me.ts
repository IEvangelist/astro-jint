import type { APIRoute } from 'astro';

// The middleware sends this route to /rewrite-target, so it must never answer.
export const GET: APIRoute = () => new Response('should not reach here');
