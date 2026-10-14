import { createApp } from 'astro/app/entrypoint';

const app = createApp({ streaming: false });

export default {
  async fetch(request) {
    return app.render(request, { addCookieHeader: true });
  },
};
