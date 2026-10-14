import type { APIRoute } from 'astro';

export const prerender = false;

export const GET: APIRoute = (ctx) => {
	return Response.json({ clientAddress: ctx.clientAddress });
};
