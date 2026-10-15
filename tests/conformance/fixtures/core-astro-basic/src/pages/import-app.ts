export async function GET() {
  try {
    const app = await import('astro/app');
    return Response.json({ imported: true, exports: Object.keys(app).sort() });
  } catch (error) {
    return Response.json({ imported: false, error: String(error) }, { status: 500 });
  }
}
