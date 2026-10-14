import type { APIRoute } from 'astro';

export const GET: APIRoute = (ctx) =>
  Response.json({
    cookiesExist: !!ctx.cookies,
    requestExist: !!ctx.request,
    redirectExist: !!ctx.redirect,
    propsExist: !!ctx.props,
    params: ctx.params,
    site: ctx.site?.toString(),
    generator: ctx.generator,
    url: ctx.url.toString(),
    clientAddress: ctx.clientAddress,
  });
