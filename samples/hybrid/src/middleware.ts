import { defineMiddleware } from 'astro:middleware';

export const onRequest = defineMiddleware(async (context, next) => {
  // `astro dev` and the build have no ASP.NET Core host, so nothing fills the locals there.
  context.locals.traceId ??= 'astro-dev';
  context.locals.user ??= null;

  if (context.isPrerendered) {
    return next();
  }

  const startedAt = Date.now();
  const response = await next();
  response.headers.set('X-Request-Id', context.locals.traceId);
  response.headers.set('Server-Timing', `astro;dur=${Date.now() - startedAt}`);
  return response;
});
