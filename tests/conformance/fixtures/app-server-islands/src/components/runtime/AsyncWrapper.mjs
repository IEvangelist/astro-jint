import { createComponent, render, renderSlot } from 'astro/runtime/server/index.js';

export default createComponent({
  factory(result, _props, slots) {
    return render`<section>${renderSlot(result, slots.default)}</section>`;
  },
  propagation: 'in-tree',
});
