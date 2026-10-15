import type { MiddlewareHandler } from 'astro';

// Rewraps the response of every path but "/" with a 404 status.
export const onRequest: MiddlewareHandler = async (context, next) => {
  const response = await next();
  if (context.url.pathname !== '/') {
    return new Response(response.body, { status: 404, headers: response.headers });
  }
  return response;
};
