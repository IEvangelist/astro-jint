import type { APIRoute } from 'astro';

// Answers every method: what is under test is the origin check Astro runs in front of it.
export const ALL: APIRoute = () => new Response('ok');
