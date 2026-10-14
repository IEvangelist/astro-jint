import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The redirect routes of the App.render() unit tests under packages/astro/test/units/redirects (render, open-redirect and
// redirect-page), which build theirs inline, as configured redirects. A redirect whose destination is a page of the
// project (/target, /gen/[id], /prefix/[...slug], /[...slug], /) has a redirectRoute and is generated from its segments;
// the other ones have their params substituted into the destination string.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
    redirects: {
      '/source': '/target',
      '/temp': { status: 302, destination: '/target' },
      '/spaces': '/target with spaces',
      '/home': '/',
      '/plain': '/destination',
      '/object-plain': { status: 301, destination: '/new-destination' },

      '/relative-slash': '/path',
      '/relative-dot': './path',
      '/relative-up': '../path',
      '/relative-bare': 'path',
      '/object-relative-slash': { status: 301, destination: '/path' },
      '/object-relative-dot': { status: 302, destination: './path' },

      '/external-http-root': 'http://example.com',
      '/external-http': 'http://example.com/path',
      '/external': 'https://example.com',
      '/external-path': 'https://example.com/path',
      '/external-page': 'https://example.com/page',
      '/external-object-https': { status: 301, destination: 'https://example.com' },
      '/external-object-http': { status: 302, destination: 'http://example.com' },
      '/evil-http': 'http://evil.com',
      '/evil-https': 'https://evil.com',
      '/protocol-relative': '//evil.com',
      '/protocol-relative-path': '//evil.com/path',
      '/protocol-relative-object': { status: 301, destination: '//evil.com' },

      '/simple/[slug]': '/[slug]/page',
      '/spread/[...rest]': '/[...rest]',
      '/articles/[slug]': '/news/[slug]',
      '/multi/[param1]/[param2]': '/moved/[param1]/[param2]',
      '/rest/[...rest]': '/moved-rest/[...rest]',
      '/city/[city]': '/moved-city/[city]',
      '/dollar/[slug]': '/moved-dollar/[slug]',
      '/dollar-rest/[...rest]': '/moved-dollar/[...rest]',

      '/old-gen/[id]': '/gen/[id]',
      '/old/[...slug]': '/[...slug]',
      '/old-prefix/[...slug]': '/prefix/[...slug]',
    },
  }),
);
