import type { APIContext, APIRoute } from 'astro';
import { ActionError, actions, getActionPath, isActionError, isInputError } from 'astro:actions';

export const prerender = false;

// The helpers of astro:actions that the unit tests of Astro (action-error, action-path and actions-proxy) call directly,
// run inside the host: GET /action-api.json?probe=<name>&... answers with what the helper gives, as JSON. ActionInputError
// and createActionsProxy are not exported, so they are reached through ActionError.fromJson() and the `actions` proxy.

function describeError(error: any) {
  return {
    isActionError: error instanceof ActionError,
    isError: error instanceof Error,
    isInputError: isInputError(error),
    type: error.type,
    code: error.code,
    status: error.status,
    message: error.message,
    stack: error.stack,
    issues: Array.isArray(error.issues) ? error.issues : null,
    fields: error.fields ?? null,
  };
}

// The values that the tests of isActionError and isInputError check.
const values: Record<string, () => unknown> = {
  'action-error': () => new ActionError({ code: 'BAD_REQUEST' }),
  'input-error': () => ActionError.fromJson({ type: 'AstroActionInputError', issues: [] }),
  'input-error-with-issue': () =>
    ActionError.fromJson({
      type: 'AstroActionInputError',
      issues: [{ code: 'invalid_type', message: 'bad', path: ['x'] }],
    }),
  'plain-action-error': () => ({ type: 'AstroActionError', code: 'NOT_FOUND' }),
  'plain-input-error': () => ({ type: 'AstroActionInputError', issues: [{ path: ['x'], message: 'bad' }] }),
  'plain-input-error-without-issues': () => ({ type: 'AstroActionInputError' }),
  error: () => new Error('nope'),
  null: () => null,
  undefined: () => undefined,
  string: () => 'error',
};

const probes: Record<string, (params: URLSearchParams, context: APIContext) => unknown> = {
  // new ActionError({ code, message, stack }): message and stack are optional.
  construct: (params) =>
    describeError(
      new ActionError({
        code: params.get('code') as any,
        message: params.get('message') ?? undefined,
        stack: params.get('stack') ?? undefined,
      }),
    ),
  // ActionError.fromJson(JSON.parse(json))
  fromJson: (params) => describeError(ActionError.fromJson(JSON.parse(params.get('json')!))),
  // ActionError.codeToStatus(code) for each code of the list, and ActionError.statusToCode(status) for each status.
  codeToStatus: (params) =>
    Object.fromEntries(params.get('codes')!.split(',').map((code) => [code, ActionError.codeToStatus(code as any)])),
  statusToCode: (params) =>
    Object.fromEntries(
      params.get('statuses')!.split(',').map((status) => [status, ActionError.statusToCode(Number(status))]),
    ),
  // isActionError(value) and isInputError(value) for one of the values above.
  is: (params) => {
    const value = values[params.get('value')!]();
    return { isActionError: isActionError(value), isInputError: isInputError(value) };
  },
  // The action function of `actions.<keys>` (comma separated, so a key can have dots in it), as a page sees it.
  proxy: (params) => {
    const action = params.get('keys')!.split(',').reduce((node: any, key) => node[key], actions) as any;
    return {
      toString: action.toString(),
      queryString: action.queryString,
      formAction: action.$$FORM_ACTION(),
      path: getActionPath(action),
    };
  },
  // actions.<name>.orThrow() through callAction(): the data, or the error that it throws.
  orThrow: async (params, context) => {
    const name = params.get('name');
    try {
      const data =
        name === 'subscribe'
          ? await context.callAction((actions as any).subscribe.orThrow, { channel: 'test' })
          : await context.callAction((actions as any).getUserOrThrow.orThrow, new FormData());
      return { data };
    } catch (error) {
      return { threw: true, ...describeError(error) };
    }
  },
};

export const GET: APIRoute = async (context) => {
  const probe = probes[context.url.searchParams.get('probe') ?? ''];
  if (!probe) {
    return new Response('Unknown probe', { status: 404 });
  }
  return Response.json(await probe(context.url.searchParams, context));
};
