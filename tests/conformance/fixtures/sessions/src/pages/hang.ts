import type { APIRoute } from 'astro';

// Not part of the upstream fixture: never answers, so the host gives up on the engine that renders it.
export const GET: APIRoute = () => new Promise<Response>(() => {});
