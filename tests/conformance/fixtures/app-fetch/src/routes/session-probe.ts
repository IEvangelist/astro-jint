import type { APIRoute } from 'astro';

// Reports whether the context has a `session` property, and what it holds, when no session storage is configured.
export const GET: APIRoute = (context) =>
  Response.json({ hasSession: 'session' in context, session: context.session === undefined ? 'undefined' : 'defined' });
