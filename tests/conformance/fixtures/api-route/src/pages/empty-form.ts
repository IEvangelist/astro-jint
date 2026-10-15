// Not part of the upstream fixture. A form without fields posts an empty body, and the script has to
// see its Content-Type like it does behind the Node adapter, or formData() throws.
export async function POST({ request }: { request: Request }) {
	const form = await request.formData();

	return new Response(JSON.stringify({ contentType: request.headers.get('content-type'), fields: [...form.keys()] }), {
		headers: { 'Content-Type': 'application/json' }
	});
}
