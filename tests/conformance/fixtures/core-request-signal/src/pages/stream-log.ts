import type { APIContext } from 'astro';

export const GET = ({ request }: APIContext) => {
	console.log('stream-log: started, aborted=' + request.signal.aborted);

	const stream = new ReadableStream<Uint8Array>({
		start(controller) {
			request.signal.addEventListener(
				'abort',
				() => {
					console.log('stream-log: aborted');
					controller.close();
				},
				{ once: true },
			);
			controller.enqueue(new TextEncoder().encode('ready'));
		},
	});

	return new Response(stream, { headers: { 'content-type': 'text/plain; charset=utf-8' } });
};
