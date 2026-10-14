import type { APIRoute } from 'astro';

export const GET: APIRoute = (ctx) => ctx.rewrite('/to-endpoint');
