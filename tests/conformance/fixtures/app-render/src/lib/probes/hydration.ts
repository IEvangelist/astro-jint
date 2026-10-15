import { extractDirectives, generateHydrateScript } from 'astro/runtime/server/hydration.js';
import { renderElement } from 'astro/runtime/server/render/util.js';

// The calls of hydration.test.ts. A case answers what its test asserts on; the cases of extractDirectives that look at one key of
// `props` answer a boolean, because a probe can only carry JSON (the symbol of the symbol test never leaves the engine).

const clientDirectives = new Map([
  ['load', ''],
  ['idle', ''],
  ['visible', ''],
  ['media', ''],
  ['only', ''],
]);

const extract = (props: Record<string | symbol, unknown>) => extractDirectives(props as any, clientDirectives as any);

export const cases: Record<string, () => unknown> = {
  'returns empty hydration and all props when no client: directives present': () => {
    const result = extract({ title: 'Hello', count: 42 });
    return { hydrationIsNull: result.hydration === null, props: result.props, isPage: result.isPage };
  },

  'extracts client:load directive': () => {
    const result = extract({
      'client:load': '',
      'client:component-path': '/src/Button.jsx',
      'client:component-export': 'default',
      title: 'Hello',
    });
    return {
      hydrationIsNull: result.hydration === null,
      directive: result.hydration?.directive,
      value: result.hydration?.value,
      componentUrl: result.hydration?.componentUrl,
      componentExport: result.hydration?.componentExport?.value,
      props: result.props,
    };
  },

  'extracts client:idle directive': () =>
    extract({
      'client:idle': '',
      'client:component-path': '/src/Widget.jsx',
      'client:component-export': 'Widget',
    }).hydration?.directive,

  'extracts client:visible directive': () => {
    const result = extract({
      'client:visible': { rootMargin: '200px' },
      'client:component-path': '/src/Component.jsx',
      'client:component-export': 'default',
    });
    return { directive: result.hydration?.directive, value: result.hydration?.value };
  },

  'extracts client:media directive with query value': () => {
    const result = extract({
      'client:media': '(max-width: 768px)',
      'client:component-path': '/src/Nav.jsx',
      'client:component-export': 'default',
    });
    return { directive: result.hydration?.directive, value: result.hydration?.value };
  },

  'extracts client:only directive and sets componentUrl': () => {
    const result = extract({
      'client:only': 'react',
      'client:component-path': '/src/ReactComp.jsx',
      'client:component-export': 'default',
    });
    return { directive: result.hydration?.directive, value: result.hydration?.value };
  },

  'separates client: props from regular props': () => {
    const result = extract({
      'client:load': '',
      'client:component-path': '/src/Btn.jsx',
      'client:component-export': 'default',
      class: 'btn',
      disabled: true,
    });
    return { props: result.props, hasClientLoad: 'client:load' in result.props };
  },

  'sets isPage = true when server:root is present': () => extract({ 'server:root': true }).isPage,

  'handles data-astro-transition-scope: included in props but excluded from propsWithoutTransitionAttributes': () => {
    const result = extract({ 'data-astro-transition-scope': 'astro-abc-1', title: 'Hello' });
    return {
      inProps: 'data-astro-transition-scope' in result.props,
      inPropsWithout: 'data-astro-transition-scope' in result.propsWithoutTransitionAttributes,
      titleInPropsWithout: 'title' in result.propsWithoutTransitionAttributes,
    };
  },

  'handles data-astro-transition-persist: excluded from propsWithoutTransitionAttributes': () => {
    const result = extract({ 'data-astro-transition-persist': 'hero', class: 'img' });
    return {
      inProps: 'data-astro-transition-persist' in result.props,
      inPropsWithout: 'data-astro-transition-persist' in result.propsWithoutTransitionAttributes,
    };
  },

  'copies symbol props to both props and propsWithoutTransitionAttributes': () => {
    const sym = Symbol('test');
    const result = extract({ [sym]: 'symbol-value' });
    return {
      props: (result.props as any)[sym],
      propsWithoutTransitionAttributes: (result.propsWithoutTransitionAttributes as any)[sym],
    };
  },

  'throws for an invalid hydration directive': () => {
    try {
      extract({ 'client:unknown': '' });
    } catch (error) {
      return { threw: true, message: (error as Error).message };
    }
    return { threw: false, message: '' };
  },

  'escapes transition directive values before rendering island attributes': async () => {
    const payload = '"><img src=x onerror=alert(1)>';
    const island = await generateHydrateScript(
      {
        renderer: { clientEntrypoint: 'renderer.js' } as any,
        result: { resolve: async (path: string) => path } as any,
        astroId: 'abc123',
        props: { 'data-astro-transition-persist': payload },
        attrs: undefined,
      },
      {
        hydrate: 'load',
        componentUrl: 'component.jsx',
        componentExport: { value: 'default' },
        displayName: 'Island',
      } as any,
    );
    return { html: renderElement('astro-island', island, false), payload };
  },

  'coerces renderer attributes before escaping them': async () => {
    const payload = '"><img src=x onerror=alert(1)>';
    let coercions = 0;
    const value = {
      toString() {
        coercions++;
        return coercions === 1 ? 'safe' : payload;
      },
    };
    const island = await generateHydrateScript(
      {
        renderer: { clientEntrypoint: 'renderer.js' } as any,
        result: { resolve: async (path: string) => path } as any,
        astroId: 'abc123',
        props: {},
        attrs: { 'data-renderer': value } as any,
      },
      {
        hydrate: 'load',
        componentUrl: 'component.jsx',
        componentExport: { value: 'default' },
        displayName: 'Island',
      } as any,
    );
    return { html: renderElement('astro-island', island, false), coercions, payload };
  },
};
