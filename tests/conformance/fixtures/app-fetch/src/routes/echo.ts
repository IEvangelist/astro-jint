import type { APIRoute } from 'astro';

// Answers with the props of the context as JSON.
export const GET: APIRoute = ({ props }) => Response.json(props);
