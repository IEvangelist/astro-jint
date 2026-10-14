import type { APIRoute } from 'astro';

// Tells whether the context has a session at all. The sessions fixture cannot say: its middleware needs one.
export const GET: APIRoute = (context) => Response.json({ session: typeof context.session });
