# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users
Inferred from the repository and the original request; the maintainer was unavailable to confirm.

- Primary: .NET and ASP.NET Core developers, and Astro developers, who are evaluating whether to host Astro on ASP.NET Core through the `astro-adapter-jint` prototype. They open the sample to see, in a few minutes, what runs where.
- Secondary: the maintainer, for whom the site is also the end-to-end fixture of `tests/AstroJint.Sample.Tests` and the scenario set of the load tests and benchmarks.

## Product Purpose
A runnable sample of a hybrid Astro site. Some pages are prerendered at build time and served as files by ASP.NET Core; others are rendered on demand per request by Astro running inside Jint, a JavaScript interpreter for .NET. The site exists to make that split observable: each page can state whether it was generated at build time or rendered on demand, and several pages show request-time behavior (clock, cookies, forms, sessions, identity, environment).

Success is a visitor who understands the hybrid model after one visit, and a sample that keeps passing its own tests.

## Positioning
Astro's on-demand runtime runs inside the ASP.NET Core process, with no Node.js process at request time. ASP.NET Core keeps ownership of the things a .NET host already does well (sessions, authentication, forwarded headers, static files, caching headers, health checks) while Astro owns rendering.

## Operating Context
- Build: `pnpm build` produces `dist/client` (static files, hashed assets under `/_astro/`), `dist/server` and `astro-jint.json`.
- Run: `dotnet run --project samples/HybridHost` serves it with `app.UseAstro()`.
- Astro 7.3.8, .NET 10, a preview of Jint 5. The repository is a prototype with documented limits and open conformance gaps.
- The same routes are exercised by `tests/AstroJint.Sample.Tests` and by the load-test and benchmark scenarios under `benchmarks/`.

## Capabilities and Constraints
- Static pages: `/`, `/about`, `/blog`, `/blog/{slug}` (three posts), `/products`.
- On-demand pages: `/products/{id}` (three products, `?currency=eur|usd`), `/time`, `/contact` (POST form with validation, cookie, redirect), `/session`, `/me` (sign in and out links `/sign-in/ada`, `/sign-out`), `/diagnostics`, plus `/api/time`, `/api/echo` and the 404 and 500 pages. `/news` redirects to `/blog`. The home page embeds a server island that shows the server time.
- Fixed by tests and therefore not negotiable in any redesign: routes, slugs and product ids; the exact `h1` text of each page (the home page `h1` is `Hybrid Astro on Jint`); the `footer .badge[data-mode]` marker (`static` or `on-demand`); the element ids and exact texts that the tests read (`#built-at`, `#server-clock`, `#rendered-at`, `#request-id`, `#price`, `#thanks`, `#name-error`, `#message-error`, `#visits`, `#user`, `#trace-id`, `#greeting`, `#api-token`, `#client-address`, `#url`); the form field names; the cache and header behavior; no extra `/blog/...` links inside `main` on the blog index; stylesheet and script assets that stay hashed files under `/_astro/`.
- No client-side framework. Plain Astro, one small bundled script on `/time`.
- Undecided: whether the sample should ever show benchmark numbers (today it shows none).

## Brand Commitments
The maintainer asked for "a modern look" and called the previous look "terrible". Recorded as given, not expanded. The page title pattern is `{page} | Hybrid Astro on Jint`.

## Evidence on Hand
- Real routes, real behavior and real data only: three blog posts and three products in `src/data/`, all authored for this sample.
- The repository documents measured throughput and the conformance status in `README.md` and `docs/`; none of that is copied into the sample.
- Absent, and not to be fabricated: customers, testimonials, logos, pricing, photography, illustrations, a product logo. The only mark is the compass star in the header, which is the same drawing as `public/favicon.svg`.

## Product Principles
1. Show the mechanism. Every page is honest about where it was produced, and the evidence (build time stamp, server clock, request id) is real.
2. The sample is a contract. What it shows is exactly what the end-to-end tests verify; nothing on it claims more than the adapter does today.
3. Name the split of responsibility. Astro renders; ASP.NET Core owns the session, the identity, the headers and the files.
4. Be light by default. The sample is a demonstration of speed, so it ships no framework runtime and no unneeded assets.
5. Keep it truthful about being a prototype.

## Accessibility & Inclusion
Inferred default, no product-specific requirement was stated: aim for WCAG 2.2 AA (contrast, keyboard and focus, reduced motion, readable on small screens, works with scripts disabled except the `/time` button).
