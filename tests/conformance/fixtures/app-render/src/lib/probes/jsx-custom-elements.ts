import { renderJSX } from 'astro/runtime/server/jsx.js';

// The calls of jsx-custom-elements.test.ts. The test takes the client directives from core/client-directive, which `astro` does not
// export; the map here has the same five names, and no vnode of these tests uses a client: directive anyway.

function createVNode(type: string, props: Record<string, any> = {}) {
  return {
    type,
    props,
    ['astro:jsx']: true,
  };
}

function createMockResult(renderers: any[] = []): any {
  return {
    pathname: '/test',
    renderers,
    clientDirectives: new Map([
      ['load', ''],
      ['idle', ''],
      ['visible', ''],
      ['media', ''],
      ['only', ''],
    ]),
    compressHTML: false,
    partial: false,
    cancelled: false,
    _metadata: {
      propagators: new Set(),
      hasHydrationScript: false,
      hasDirectives: new Set(),
      hasRenderedHead: false,
      rendererSpecificHydrationScripts: new Set(),
    },
  };
}

export const cases: Record<string, () => unknown> = {
  'routes standard HTML elements through renderElement': async () => {
    const result = createMockResult();
    const vnode = createVNode('div', { class: 'test', children: 'Hello' });
    return String(await renderJSX(result, vnode));
  },

  'routes custom elements (with hyphens) through renderComponentToString for renderer pipeline': async () => {
    let rendererCheckCalled = false;
    let rendererRenderCalled = false;

    const mockRenderer = {
      name: 'test-renderer',
      ssr: {
        check: async (Component: unknown) => {
          rendererCheckCalled = true;
          return typeof Component === 'string' && Component.includes('-');
        },
        renderToStaticMarkup: async (Component: unknown, props: Record<string, any>) => {
          rendererRenderCalled = true;
          const attrs = Object.entries(props || {})
            .map(([k, v]) => ` ${k}="${v}"`)
            .join('');
          return {
            html: `<${Component}${attrs} data-ssr="true"></${Component}>`,
          };
        },
        supportsAstroStaticSlot: false,
      },
    };

    const result = createMockResult([mockRenderer]);
    const vnode = createVNode('my-element', { greeting: 'hello' });
    const output = String(await renderJSX(result, vnode));
    return { output, rendererCheckCalled, rendererRenderCalled };
  },

  'falls back to raw HTML for custom elements when no renderer claims them': async () => {
    const mockRenderer = {
      name: 'test-renderer',
      ssr: {
        check: async () => false,
        renderToStaticMarkup: async () => ({ html: '' }),
        supportsAstroStaticSlot: false,
      },
    };

    const result = createMockResult([mockRenderer]);
    const vnode = createVNode('unknown-element', { foo: 'bar', children: 'content' });
    return String(await renderJSX(result, vnode));
  },

  'preserves slot attribute on children of custom elements': async () => {
    const result = createMockResult();
    const childVNode = createVNode('svg', { slot: 'icon', xmlns: 'http://www.w3.org/2000/svg' });
    const vnode = createVNode('my-element', { children: [childVNode, 'text content'] });
    return String(await renderJSX(result, vnode));
  },

  'does not route standard elements through the renderer pipeline': async () => {
    let rendererCheckCalled = false;

    const mockRenderer = {
      name: 'test-renderer',
      ssr: {
        check: async () => {
          rendererCheckCalled = true;
          return false;
        },
        renderToStaticMarkup: async () => ({ html: '' }),
        supportsAstroStaticSlot: false,
      },
    };

    const result = createMockResult([mockRenderer]);
    const vnode = createVNode('div', { class: 'test', children: 'Hello' });
    await renderJSX(result, vnode);
    return rendererCheckCalled;
  },
};
