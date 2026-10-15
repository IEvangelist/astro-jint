import { defineMiddleware } from 'astro:middleware';

// Astro refuses to build a project with `i18n.routing: 'manual'` that has no user middleware, and the middleware the
// variants list (src/middleware-library) are added by the config. This one does nothing.
export const onRequest = defineMiddleware((_context, next) => next());
