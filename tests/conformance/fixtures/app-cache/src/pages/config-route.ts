import type { APIRoute } from 'astro';

// Does not call cache.set(): its cache options come from the route rules of the config (the 'cdn' build).
export const GET: APIRoute = () => Response.json({ fromConfig: true });
