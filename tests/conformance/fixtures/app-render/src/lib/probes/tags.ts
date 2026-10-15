import { renderUniqueStylesheet } from 'astro/runtime/server/render/tags.js';

// The calls of tags.test.ts, with its arguments.

function createResult() {
  return { styles: new Set() } as any;
}

export const cases: Record<string, () => unknown> = {
  'renders inline style without attributes by default': () =>
    renderUniqueStylesheet(createResult(), { type: 'inline', content: '.foo { color: red; }' }),

  'renders inline style with data-vite-dev-id when viteDevId is provided': () =>
    renderUniqueStylesheet(createResult(), {
      type: 'inline',
      content: '.bar { color: blue; }',
      viteDevId: '/src/components/Bar.svelte?svelte&type=style&lang.css',
    }),

  'omits data-vite-dev-id when viteDevId is undefined': () =>
    renderUniqueStylesheet(createResult(), {
      type: 'inline',
      content: '.baz { color: green; }',
      viteDevId: undefined,
    }),
};
