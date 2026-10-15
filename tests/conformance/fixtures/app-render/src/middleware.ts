import { defineMiddleware } from 'astro:middleware';

// What the middleware of the astro-global fixture of upstream does: it stashes `context.routePattern` and
// `context.isPrerendered` into the locals, so that a page can show both.
export const onRequest = defineMiddleware(async (context, next) => {
  Object.assign(context.locals as Record<string, unknown>, {
    localsPattern: context.routePattern,
    localsPrerendered: context.isPrerendered,
  });
  return next();
});
