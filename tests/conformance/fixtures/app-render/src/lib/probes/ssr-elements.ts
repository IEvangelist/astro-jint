import {
  createAssetLink,
  createModuleScriptElement,
  createStylesheetElementSet,
} from '../../../node_modules/astro/dist/core/render/ssr-element.js';

// The calls of ssr-elements.test.ts, with its arguments. `astro` does not export core/render/ssr-element.js, so it is imported by
// its path inside the installed package; the bundle of the host then runs the very file that builds the <link> and <script>
// elements of every page. A case answers what its test asserts on (the elements are plain objects).

function firstOf<T>(set: Set<T>): T {
  return [...set][0];
}

export const cases: Record<string, () => unknown> = {
  // createAssetLink
  'returns href unchanged when no base or prefix': () => createAssetLink('/_astro/style.css'),
  'prepends base path': () => createAssetLink('/_astro/style.css', '/docs/'),
  'prepends string assetsPrefix': () => createAssetLink('/_astro/style.css', '/', 'https://cdn.com'),
  'uses css-specific prefix from per-type object': () =>
    createAssetLink('/_astro/style.css', '/', { css: 'https://css.cdn.com', fallback: 'https://cdn.com' } as any),
  'falls back to fallback prefix for unknown extensions': () =>
    createAssetLink('/_astro/image.png', '/', { css: 'https://css.cdn.com', fallback: 'https://cdn.com' } as any),
  'uses js-specific prefix for .js files': () =>
    createAssetLink('/_astro/app.js', '/', { js: 'https://js.cdn.com', fallback: 'https://cdn.com' } as any),
  'appends query params': () =>
    createAssetLink('/_astro/style.css', '/', undefined, new URLSearchParams({ v: '123' })),
  'preserves hash fragment and appends query params before it': () =>
    createAssetLink('/_astro/style.css#inline', '/', undefined, new URLSearchParams({ v: '1' })),
  'preserves hash fragment without query params': () => createAssetLink('/_astro/style.css#inline', '/'),

  // createStylesheetElementSet
  'creates an inline style element': () => {
    const el = firstOf(createStylesheetElementSet([{ type: 'inline', content: 'body{color:red}' }] as any));
    return { children: el.children, props: el.props };
  },
  'creates an external link element with rel=stylesheet': () => {
    const el = firstOf(createStylesheetElementSet([{ type: 'external', src: '/_astro/style.css' }] as any));
    return { rel: el.props.rel, children: el.children };
  },
  'applies base path to external stylesheet href': () =>
    firstOf(createStylesheetElementSet([{ type: 'external', src: '/_astro/style.css' }] as any, '/docs/')).props
      .href,
  'applies assetsPrefix to external stylesheet href \u2014 Migrated from astro-assets-prefix.test.js': () =>
    firstOf(
      createStylesheetElementSet([{ type: 'external', src: '/_astro/style.css' }] as any, '/', 'https://cdn.com'),
    ).props.href,
  'applies css-specific prefix \u2014 Migrated from astro-assets-prefix-multi-cdn.test.js': () =>
    firstOf(
      createStylesheetElementSet([{ type: 'external', src: '/_astro/style.css' }] as any, '/', {
        css: 'https://css.cdn.com',
        fallback: 'https://cdn.com',
      } as any),
    ).props.href,
  'handles mixed inline and external stylesheets': () =>
    createStylesheetElementSet([
      { type: 'inline', content: 'body{}' },
      { type: 'external', src: '/_astro/style.css' },
    ] as any).size,

  // createModuleScriptElement
  'creates an external module script with type=module and src': () => {
    const el = createModuleScriptElement({ type: 'external', value: '/_astro/app.js' } as any);
    return { type: el.props.type, src: el.props.src, children: el.children };
  },
  'creates an inline module script with type=module and children': () => {
    const el = createModuleScriptElement({ type: 'inline', value: 'console.log(1)' } as any);
    return { type: el.props.type, children: el.children, srcIsUndefined: el.props.src === undefined };
  },
  'applies string assetsPrefix to external script src \u2014 Migrated from astro-assets-prefix.test.js': () =>
    createModuleScriptElement({ type: 'external', value: '/_astro/app.js' } as any, '/', 'https://cdn.com').props
      .src,
  'applies js-specific prefix to external script src \u2014 Migrated from astro-assets-prefix-multi-cdn.test.js': () =>
    createModuleScriptElement({ type: 'external', value: '/_astro/app.js' } as any, '/', {
      js: 'https://js.cdn.com',
      fallback: 'https://cdn.com',
    } as any).props.src,
  'applies base path to external script src': () =>
    createModuleScriptElement({ type: 'external', value: '/_astro/app.js' } as any, '/docs/').props.src,
};
