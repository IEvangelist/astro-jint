import type { APIRoute } from 'astro';

export const GET: APIRoute = ({ params }) => new Response(`catch-all:${params.slug}`);
