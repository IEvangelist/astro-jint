import type { MiddlewareHandler } from 'astro';

// Rewrites every request to the root.
export const onRequest: MiddlewareHandler = async (_context, next) => {
  const response = await next('/');
  return response;
};
