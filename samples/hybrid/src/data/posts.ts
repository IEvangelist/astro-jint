export interface Post {
  slug: string;
  title: string;
  summary: string;
  body: string[];
}

export const posts: Post[] = [
  {
    slug: 'hello-jint',
    title: 'Hello, Jint',
    summary: 'Why run Astro inside an ASP.NET Core process.',
    body: [
      'Astro renders most of this site at build time. The pages that need a request are rendered on demand by the same Astro code, running in the Jint JavaScript interpreter inside the ASP.NET Core host.',
      'There is no Node.js process to deploy, supervise or secure next to the .NET application.',
    ],
  },
  {
    slug: 'static-meets-dynamic',
    title: 'Static meets dynamic',
    summary: 'One site, two ways to render a page.',
    body: [
      'A page is static unless it sets export const prerender = false. Static pages are plain files, so the host serves them without touching JavaScript.',
      'Everything else, from product pages to forms and API endpoints, goes through the engine pool.',
    ],
  },
  {
    slug: 'engine-pool',
    title: 'The engine pool',
    summary: 'How requests share JavaScript engines.',
    body: [
      'Each on-demand request borrows an engine from a pool, renders, and gives it back. The pool size is the number of pages that can render at the same time.',
      'A request that waits too long for an engine gets a 503, and one that renders for too long gets a 504.',
    ],
  },
];
