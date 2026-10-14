export const GET = ({ url }) => Response.json({ ok: true, query: url.search });

export const POST = async ({ request }) =>
  Response.json({
    method: request.method,
    contentType: request.headers.get('content-type'),
    body: await request.text(),
  });
