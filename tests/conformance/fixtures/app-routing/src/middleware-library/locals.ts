import type { MiddlewareHandler } from 'astro';

export const onRequest: MiddlewareHandler = async (context, next) => {
  (context.locals as Record<string, unknown>).message = 'from middleware';
  return next();
};
