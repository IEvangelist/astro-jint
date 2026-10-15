import { defineAction } from 'astro:actions';
import { counter } from '../action-counter';

// The deleteAccount action of packages/astro/test/units/actions/action-origin-check.test.ts: a form action that counts the
// times its handler runs, which the route action-runs.ts reports.
export const server = {
  deleteAccount: defineAction({
    accept: 'form',
    handler: async () => {
      counter.runs++;
      return { deleted: 1 };
    },
  }),
};
