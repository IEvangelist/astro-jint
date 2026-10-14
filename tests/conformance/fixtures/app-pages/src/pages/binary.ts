import type { APIRoute } from 'astro';

export const POST: APIRoute = async ({ request }) => {
  const data = await request.formData();
  const file = data.get('file') as File | null;
  if (file && (await file.arrayBuffer()).byteLength > 0) {
    return new Response('ok', { status: 200 });
  }
  return new Response(null, { status: 400 });
};
