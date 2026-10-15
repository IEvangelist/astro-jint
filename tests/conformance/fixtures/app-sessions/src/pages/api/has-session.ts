import type { APIRoute } from 'astro';

// `context.session` is defined when the build has a session store and undefined for `session: false`.
export const GET: APIRoute = ({ session }) => Response.json({ hasSession: session !== undefined });
