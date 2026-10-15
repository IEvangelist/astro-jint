import { createComponent, HTMLString, renderComponent, renderTemplate } from 'astro/runtime/server/index.js';

// The calls of rendering.test.ts, with the components and the arguments of the unit tests. They build their components with
// createComponent() and render them to a string with the render() methods of the runtime, so they need no route. A case is named
// after its test.

const DEFAULT_RESULT = { clientDirectives: new Map() } as any;

const isPromise = (value: unknown): boolean => value instanceof Promise;

// A fresh list of the components that were evaluated and the leaf component of every scenario (the unit test resets it before each test).
function scenario() {
  const evaluated: string[] = [];
  const Scalar = createComponent((_result: any, props: { id: string }) => {
    evaluated.push(props.id);
    return renderTemplate`<scalar id="${props.id}"></scalar>`;
  });
  return { evaluated, Scalar };
}

function renderToString(item: any): string | Promise<string> {
  if (isPromise(item)) {
    return (item as Promise<unknown>).then(renderToString);
  }

  let result = '';
  const destination = {
    write: (chunk: unknown) => {
      result += String(chunk);
    },
  };

  const renderResult = item.render(destination);
  if (isPromise(renderResult)) {
    return renderResult.then(() => result);
  }

  return result;
}

const getRenderedIds = (html: string): string[] => [...html.matchAll(/\sid="([^"]*)"/g)].map((match) => match[1]);

function manualResetEvent() {
  let done = false;
  let resolve: (() => void) | undefined;
  let promise: Promise<void> | undefined;
  return {
    release() {
      if (done) {
        return;
      }
      done = true;
      resolve?.();
    },
    wait(): Promise<void> {
      if (!promise) {
        promise = done
          ? Promise.resolve()
          : new Promise<void>((r) => {
              resolve = r;
            });
      }
      return promise;
    },
  };
}

export const cases: Record<string, () => unknown> = {
  'components are evaluated and rendered depth-first': async () => {
    const { evaluated, Scalar } = scenario();

    const Root = createComponent((result: any, props: { id: string }) => {
      evaluated.push(props.id);
      return renderTemplate`<root id="${props.id}">
				${renderComponent(result, '', Scalar, { id: `${props.id}/scalar_1` })}
				${renderComponent(result, '', Nested, { id: `${props.id}/nested` })}
				${renderComponent(result, '', Scalar, { id: `${props.id}/scalar_2` })}
			</root>`;
    });

    const Nested = createComponent((result: any, props: { id: string }) => {
      evaluated.push(props.id);
      return renderTemplate`<nested id="${props.id}">
				${renderComponent(result, '', Scalar, { id: `${props.id}/scalar` })}
			</nested>`;
    });

    const result = await renderToString(Root(DEFAULT_RESULT, { id: 'root' }, {}));
    return { evaluated, rendered: getRenderedIds(result) };
  },

  'synchronous component trees are rendered without promises': () => {
    const { evaluated, Scalar } = scenario();

    const Root = createComponent((result: any, props: { id: string }) => {
      evaluated.push(props.id);
      return renderTemplate`<root id="${props.id}">
				${() => renderComponent(result, '', Scalar, { id: `${props.id}/scalar_1` })}
				${function* () {
          yield renderComponent(result, '', Scalar, { id: `${props.id}/scalar_2` });
        }}
				${[renderComponent(result, '', Scalar, { id: `${props.id}/scalar_3` })]}
				${renderComponent(result, '', Scalar, { id: `${props.id}/scalar_4` })}
			</root>`;
    });

    const result = renderToString(Root(DEFAULT_RESULT, { id: 'root' }, {}));
    const resultIsPromise = isPromise(result);
    return {
      isPromise: resultIsPromise,
      evaluated,
      rendered: resultIsPromise ? [] : getRenderedIds(result as string),
    };
  },

  'async component children are deferred': async () => {
    const { evaluated, Scalar } = scenario();

    const Root = createComponent((result: any, props: { id: string }) => {
      evaluated.push(props.id);
      return renderTemplate`<root id="${props.id}">
				${renderComponent(result, '', AsyncNested, { id: `${props.id}/asyncnested` })}
				${renderComponent(result, '', Scalar, { id: `${props.id}/scalar` })}
			</root>`;
    });

    const AsyncNested = createComponent(async (result: any, props: { id: string }) => {
      evaluated.push(props.id);
      await new Promise((resolve) => setTimeout(resolve, 0));
      return renderTemplate`<asyncnested id="${props.id}">
				${renderComponent(result, '', Scalar, { id: `${props.id}/scalar` })}
			</asyncnested>`;
    });

    const result = await renderToString(Root(DEFAULT_RESULT, { id: 'root' }, {}));
    return { evaluated, rendered: getRenderedIds(result) };
  },

  'adjacent async components are evaluated eagerly': async () => {
    const { evaluated, Scalar } = scenario();
    const resetEvent = manualResetEvent();

    const Root = createComponent((result: any, props: { id: string }) => {
      evaluated.push(props.id);
      return renderTemplate`<root id="${props.id}">
				${renderComponent(result, '', AsyncNested, { id: `${props.id}/asyncnested_1` })}
				${renderComponent(result, '', AsyncNested, { id: `${props.id}/asyncnested_2` })}
			</root>`;
    });

    const AsyncNested = createComponent(async (result: any, props: { id: string }) => {
      evaluated.push(props.id);
      await resetEvent.wait();
      return renderTemplate`<asyncnested id="${props.id}">
				${renderComponent(result, '', Scalar, { id: `${props.id}/scalar` })}
			</asyncnested>`;
    });

    const awaitableResult = renderToString(Root(DEFAULT_RESULT, { id: 'root' }, {}));
    const evaluatedBeforeRelease = [...evaluated];

    resetEvent.release();

    // relinquish control after release
    await new Promise((resolve) => setTimeout(resolve, 0));
    const evaluatedAfterRelease = [...evaluated];

    const result = await awaitableResult;
    return { evaluatedBeforeRelease, evaluatedAfterRelease, rendered: getRenderedIds(result) };
  },

  'skip rendering blank html fragments': async () => {
    const Root = createComponent(() => {
      const message = 'hello world';
      return renderTemplate`${message}`;
    });

    const renderInstance = await renderComponent(DEFAULT_RESULT, '', Root, {});

    const chunks: unknown[] = [];
    const destination = {
      write: (chunk: unknown) => {
        chunks.push(chunk);
      },
    };

    await renderInstance.render(destination);

    return chunks.map((chunk) => ({ isHTMLString: chunk instanceof HTMLString, text: String(chunk) }));
  },

  'all primitives are rendered in order': async () => {
    const { evaluated, Scalar } = scenario();

    const Root = createComponent((result: any, props: { id: string }) => {
      evaluated.push(props.id);
      return renderTemplate`<root id="${props.id}">
				${renderComponent(result, '', Scalar, { id: `${props.id}/first` })}
				${() => renderComponent(result, '', Scalar, { id: `${props.id}/func` })}
				${new Promise((resolve) => {
          setTimeout(() => {
            resolve(renderComponent(result, '', Scalar, { id: `${props.id}/promise` }));
          }, 0);
        })}
				${[
          () => renderComponent(result, '', Scalar, { id: `${props.id}/array_func` }),
          renderComponent(result, '', Scalar, { id: `${props.id}/array_scalar` }),
        ]}
				${async function* () {
          yield await new Promise((resolve) => {
            setTimeout(() => {
              resolve(renderComponent(result, '', Scalar, { id: `${props.id}/async_generator` }));
            }, 0);
          });
        }}
				${function* () {
          yield renderComponent(result, '', Scalar, { id: `${props.id}/generator` });
        }}
				${renderComponent(result, '', Scalar, { id: `${props.id}/last` })}
			</root>`;
    });

    const result = await renderToString(Root(DEFAULT_RESULT, { id: 'root' }, {}));
    return { rendered: getRenderedIds(result) };
  },

  'awaits thenables that also have a render method': async () => {
    const thenable = Object.assign(Promise.resolve('expected'), {
      render(destination: { write(chunk: unknown): void }) {
        destination.write('unexpected');
      },
    });

    return await renderToString(renderTemplate`${[thenable]}`);
  },
};
