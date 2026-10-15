import { createComponent, render, renderComponent } from 'astro/runtime/server/index.js';
import AsyncContent from './AsyncContent.mjs';
import AsyncWrapper from './AsyncWrapper.mjs';

// A server island whose wrapper has a slot that only renders its content after a delay: the head content of that content has to be
// waited for.
export default createComponent(
  (result) =>
    render`${renderComponent(
      result,
      'Wrapper',
      AsyncWrapper,
      {},
      {
        default: async () => {
          await new Promise((resolve) => setTimeout(resolve, 10));
          return render`${renderComponent(result, 'Content', AsyncContent, {}, {})}`;
        },
      },
    )}`,
);
