// Used by `test/logger.test.ts` to check that a `logger.entrypoint` given as a path
// relative to the project root is resolved and bundled. `__relative` lets the test assert
// this module was actually used, rather than a built-in fallback.
export default function () {
	// Not upstream: the test sees through this marker, over HTTP, that the destination was created by the engine.
	console.log('logger-test: relative destination created');
	return { __relative: true, write() {} };
}
