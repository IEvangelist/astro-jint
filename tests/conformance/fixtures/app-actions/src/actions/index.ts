import { ActionError, defineAction } from 'astro:actions';
import { z } from 'astro/zod';

// The actions of the Actions unit tests of Astro (packages/astro/test/units/actions). The first group is the table of
// actions of actions-app.test.ts, the second the data and errors of serialize.test.ts, the third (`forms`) the schemas of
// form-data-to-object.test.ts, the last those of render-context.test.ts (render/render-context.test.ts).

// The count of increment calls, for the test that checks that `increment.orThrow` does not run the action.
let calls = 0;

// The count of runs of `countedForm`, for the tests that check whether the render of a page ran the form action it was posted.
let formCalls = 0;

const passwordSchema = z
  .string()
  .min(8, 'Password should be at least 8 chars length')
  .max(128, 'Password length exceeded. Max 128 chars.');

// What the data and the errors of serialize.test.ts are, by name.
function resultOf(kind: string): unknown {
  switch (kind) {
    case 'string':
      return 'hello';
    case 'number':
      return 42;
    case 'zero':
      return 0;
    case 'false':
      return false;
    case 'null':
      return null;
    case 'object':
      return { channel: 'bholmesdev', subscribeButtonState: 'smashed' };
    case 'complex':
      return { date: new Date('2024-01-01'), set: new Set([1, 2, 3]), url: new URL('https://astro.build') };
    case 'roundtrip':
      return { date: new Date('2024-06-15'), set: new Set(['a', 'b']) };
    case 'empty':
      return undefined;
    case 'response':
      return new Response('nope');
    case 'unauthorized':
      throw new ActionError({ code: 'UNAUTHORIZED', message: 'Not logged in' });
    default:
      throw new Error(`Unknown kind ${kind}`);
  }
}

// What the handlers of `forms` send back: the parsed input as JSON, with the files by name (undefined values drop out of JSON,
// as upstream's `assert.equal(res.email, undefined)` accepts both an absent key and an undefined value).
function show(data: unknown) {
  return JSON.stringify(data, (_key, value) => (value instanceof File ? `__File__:${value.name}` : value));
}

function formAction<T extends z.ZodType>(input: T) {
  return defineAction({ accept: 'form', input, handler: async (data) => show(data) });
}

const contact = z.discriminatedUnion('type', [
  z.object({ type: z.literal('email'), email: z.string() }),
  z.object({ type: z.literal('phone'), phone: z.string() }),
]);

export const server = {
  // actions-app.test.ts
  subscribe: defineAction({
    input: z.object({ channel: z.string() }),
    handler: async ({ channel }) => ({ channel, subscribeButtonState: 'smashed' }),
  }),
  comment: defineAction({
    accept: 'form',
    input: z.object({ channel: z.string(), comment: z.string() }),
    handler: async ({ channel, comment }) => ({ channel, comment }),
  }),
  commentPlainFormData: defineAction({
    accept: 'form',
    handler: async (formData) => ({ success: true, isFormData: formData instanceof FormData }),
  }),
  fireAndForget: defineAction({
    handler: async () => {
      return;
    },
  }),
  getUserOrThrow: defineAction({
    accept: 'form',
    handler: async () => {
      throw new ActionError({ code: 'UNAUTHORIZED', message: 'Not logged in' });
    },
  }),
  greet: defineAction({
    accept: 'form',
    input: z.object({ name: z.string() }),
    handler: async ({ name }) => `Hello, ${name}!`,
  }),
  'with space': defineAction({
    input: z.object({ name: z.string() }),
    handler: async (input) => `Hello, ${input.name}!`,
  }),
  'with/slash': defineAction({
    input: z.object({ name: z.string() }),
    handler: async (input) => `Hello, ${input.name}!`,
  }),
  increment: defineAction({
    handler: async () => {
      calls += 1;
      return { calls };
    },
  }),
  locked: defineAction({
    handler: async () => ({ safe: true }),
  }),
  nestedFormObject: defineAction({
    accept: 'form',
    input: z.object({
      a: z.string(),
      bc: z
        .object({
          b: z.enum(['hoge', 'huga']),
          c: z.string().optional(),
        })
        .superRefine((data, ctx) => {
          if (data.b === 'huga' && (!data.c || data.c.trim() === '')) {
            ctx.addIssue({
              code: 'custom',
              path: ['bc', 'c'],
              message: 'C is required when B is "huga"',
            });
          }
        }),
    }),
    handler: async (data) => data,
  }),
  nestedDiscriminatedUnion: defineAction({
    accept: 'form',
    input: z.object({
      name: z.string(),
      contact,
    }),
    handler: async (data) => data,
  }),
  validatePassword: defineAction({
    accept: 'form',
    input: z
      .object({ password: z.string(), confirmPassword: z.string() })
      .refine((data) => data.password === data.confirmPassword, {
        message: 'Passwords do not match',
      }),
    handler: async ({ password }) => password,
  }),
  validatePasswordComplex: defineAction({
    accept: 'form',
    input: z
      .object({
        currentPassword: passwordSchema,
        newPassword: passwordSchema,
        confirmNewPassword: passwordSchema,
      })
      .required()
      .refine(
        ({ newPassword, confirmNewPassword }) => newPassword === confirmNewPassword,
        'The new password confirmation does not match',
      )
      .refine(
        ({ currentPassword, newPassword }) => currentPassword !== newPassword,
        'The old password and the new password must not match',
      )
      .transform((input) => ({
        currentPassword: input.currentPassword,
        newPassword: input.newPassword,
      }))
      .pipe(
        z.object({
          currentPassword: passwordSchema,
          newPassword: passwordSchema,
        }),
      ),
    handler: async (data) => data,
  }),
  transformFormInput: defineAction({
    accept: 'form',
    input: z.instanceof(FormData).transform((formData) => Object.fromEntries(formData.entries())),
    handler: async (data) => data,
  }),
  imageUploadInChunks: defineAction({
    accept: 'form',
    input: z.discriminatedUnion('type', [
      z.object({
        type: z.literal('first-chunk'),
        image: z.instanceof(File),
        alt: z.string(),
      }),
      z.object({
        type: z.literal('rest-chunk'),
        image: z.instanceof(File),
        uploadId: z.string(),
      }),
    ]),
    handler: async (data) => {
      if (data.type === 'first-chunk') {
        return { uploadId: Math.random().toString(36).slice(2) };
      }
      return { uploadId: data.uploadId };
    },
  }),

  // serialize.test.ts: an action that returns the data, or throws the error, that its `kind` names; one that throws the
  // error of a given code; and the form variant of the first, for the page that deserializes the result.
  serialize: defineAction({
    input: z.object({ kind: z.string() }),
    handler: async ({ kind }) => resultOf(kind),
  }),
  serializeForm: defineAction({
    accept: 'form',
    input: z.object({ kind: z.string() }),
    handler: async ({ kind }) => resultOf(kind),
  }),
  throwCode: defineAction({
    input: z.object({ code: z.string() }),
    handler: async ({ code }) => {
      throw new ActionError({ code: code as ConstructorParameters<typeof ActionError>[0]['code'] });
    },
  }),

  // form-data-to-object.test.ts: one action per schema, which answers with what formDataToObject made of the form.
  forms: {
    strings: formAction(z.object({ name: z.string(), email: z.string() })),
    numbers: formAction(z.object({ age: z.number() })),
    booleans: formAction(
      z.object({
        isCool: z.boolean(),
        isNotCool: z.boolean(),
        isTrue: z.boolean(),
        isFalse: z.boolean(),
        falseString: z.boolean(),
      }),
    ),
    optional: formAction(
      z.object({ name: z.string().optional(), email: z.string().optional(), age: z.number().optional() }),
    ),
    nullable: formAction(
      z.object({ name: z.string().nullable(), email: z.string().nullable(), age: z.number().nullable() }),
    ),
    defaults: formAction(
      z.object({
        name: z.string().default('test'),
        email: z.string().default('test@test.test'),
        favoriteNumbers: z.array(z.number()).default([1, 2]),
      }),
    ),
    chained: formAction(
      z.object({
        name: z.string().default('test').optional(),
        email: z.string().optional().nullish(),
        favoriteNumbers: z.array(z.number()).default([1, 2]).nullish().optional(),
      }),
    ),
    file: formAction(z.object({ file: z.instanceof(File) })),
    files: formAction(z.object({ files: z.array(z.instanceof(File)) })),
    stringArray: formAction(z.object({ contact: z.array(z.string()) })),
    numberArray: formAction(z.object({ age: z.array(z.number()) })),
    booleanArray: formAction(z.object({ flags: z.array(z.boolean()) })),
    nested: formAction(
      z.object({
        a: z.string(),
        bc: z.object({ b: z.enum(['hoge', 'huga']), c: z.string().optional() }),
      }),
    ),
    nestedSuperRefine: formAction(
      z.object({
        a: z.string(),
        bc: z
          .object({ b: z.enum(['hoge', 'huga']), c: z.string().optional() })
          .superRefine((data, ctx) => {
            if (data.b === 'huga' && (!data.c || data.c.trim() === '')) {
              ctx.addIssue({
                code: 'custom',
                path: ['bc', 'c'],
                message: 'C is required when B is "huga"',
              });
            }
          }),
      }),
    ),
    deeplyNested: formAction(z.object({ a: z.object({ b: z.object({ c: z.string(), d: z.number() }) }) })),
    optionalNested: formAction(
      z.object({
        name: z.string(),
        address: z.object({ street: z.string(), city: z.string() }).optional(),
      }),
    ),
    nullableNested: formAction(
      z.object({
        name: z.string(),
        address: z.object({ street: z.string(), city: z.string() }).nullable(),
      }),
    ),
    defaultNested: formAction(
      z.object({
        name: z.string(),
        settings: z.object({ theme: z.string() }).default({ theme: 'dark' }),
      }),
    ),
    discriminatedUnion: formAction(z.object({ name: z.string(), contact })),
    passthrough: formAction(z.object({ expected: z.number() }).passthrough()),
  },

  // render-context.test.ts: the form action that counts its runs (the `testAction` of the two skipMiddleware tests) and the action
  // that reads the counter, and the two actions that use `context.logger`: one that tells what it is made of, one that writes.
  countedForm: defineAction({
    accept: 'form',
    handler: async () => {
      formCalls += 1;
      return 'action result';
    },
  }),
  formCallCount: defineAction({
    handler: async () => ({ calls: formCalls }),
  }),
  contextLoggerMethods: defineAction({
    handler: async (_input, context) =>
      [typeof context.logger, typeof context.logger.info, typeof context.logger.warn, typeof context.logger.error].join('/'),
  }),
  contextLoggerWrites: defineAction({
    handler: async (_input, context) => {
      context.logger.info('info message');
      context.logger.warn('warn message');
      context.logger.error('error message');
      return 'written';
    },
  }),
};
