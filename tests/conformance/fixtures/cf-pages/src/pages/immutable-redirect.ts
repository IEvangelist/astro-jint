import type { APIRoute } from 'astro';

export const prerender = false;

// Response.redirect() returns a response with immutable headers.
export const GET: APIRoute = (context) => {
	context.cookies.set('immutable', '1', { path: '/' });
	return Response.redirect('https://example.com/');
};
