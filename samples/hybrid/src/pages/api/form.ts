import type { APIRoute } from 'astro';

export const prerender = false;

export const POST: APIRoute = async ({ request, cookies }) => {
  const form = await request.formData();
  const fields: Record<string, string> = {};
  for (const [key, value] of form.entries()) {
    fields[key] = typeof value === 'string' ? value : `[file ${value.name} ${value.size}b]`;
  }
  return Response.json({ fields, cookieA: cookies.get('a')?.value ?? null });
};
