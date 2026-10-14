import type { APIRoute } from 'astro';

export const prerender = false;

export const POST: APIRoute = async ({ request, cookies }) => {
  const body = await request.text();
  cookies.set('a', '1', { path: '/' });
  cookies.set('b', '2', { path: '/', httpOnly: true });
  return new Response(
    JSON.stringify({
      method: request.method,
      contentType: request.headers.get('content-type'),
      length: body.length,
      body,
    }),
    { status: 201, headers: { 'content-type': 'application/json', 'x-spike': 'echo' } },
  );
};
