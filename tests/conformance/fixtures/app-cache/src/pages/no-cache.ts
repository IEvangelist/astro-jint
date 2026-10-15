import type { APIRoute } from 'astro';

// 'noCacheEndpoint': never touches the cache.
export const GET: APIRoute = () => Response.json({ timestamp: Date.now() });
