import { render } from 'svelte/server';
import Component from '../components/svelte/Component.svelte';

export const prerender = false;

// The call of @astrojs/svelte, without its check of the text of the component function.
export async function GET() {
  try {
    const { body } = await render(Component, { props: {} });
    return new Response(body, { headers: { 'Content-Type': 'text/html' } });
  } catch (error) {
    return new Response(String(error), { status: 500, headers: { 'Content-Type': 'text/plain' } });
  }
}
