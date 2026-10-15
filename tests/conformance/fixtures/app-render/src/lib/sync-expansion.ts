import {
  createComponent,
  render,
  renderComponent,
  renderSlotToString,
} from 'astro/runtime/server/index.js';
import { createRenderInstruction } from 'astro/runtime/server/render/instruction.js';

// The components that sync-expansion.test.ts writes inline with createComponent(). Upstream renders them with the Container API,
// the pages of src/pages/sync-expansion render them through the pipeline of the host.

const Child = createComponent((_result: any, props: any) => render`<div>${props.content}</div>`);

// A slot rendered to a string carries a render instruction (a script); a sync component then puts that string in its own template.
export const ScriptCarryingPage = createComponent(async (result: any) => {
  const content = await renderSlotToString(
    result,
    () =>
      render`<p>slot</p>${createRenderInstruction({
        type: 'script',
        id: 'sync-expansion-script',
        content: '<script type="module">/* island */</script>',
      })}`,
  );
  return render`<main>${renderComponent(result, 'Child', Child, { content }, {})}</main>`;
});

// Upstream creates the template once and renders it from two renders of a container; here it lives as long as the engine does,
// so that every request after the first one renders the same template again.
let template: unknown;

export const TwiceRenderedPage = createComponent(() => {
  template ??= render`<p>${Promise.reject(new Error('boom'))}</p>`;
  return template;
});
