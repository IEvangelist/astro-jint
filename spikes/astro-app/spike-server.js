import { createApp } from 'astro/app/entrypoint';

const app = createApp({ streaming: false });

export default {
  async fetch(request) {
    const response = await app.render(request, { addCookieHeader: true });
    // Jint's fetch-handler hosting rejects stream-backed bodies; Astro re-wraps
    // `response.body` when it merges error pages, so buffer everything here.
    if (response.body === null) {
      return response;
    }
    return new Response(await response.arrayBuffer(), {
      status: response.status,
      statusText: response.statusText,
      headers: response.headers,
    });
  },
};
