import type { APIRoute } from 'astro';
import { counter } from '../action-counter';

// How many times the handler of the deleteAccount action ran in this engine.
export const GET: APIRoute = () => Response.json({ runs: counter.runs });
