import { createComponent, createHeadAndContent, render } from 'astro/runtime/server/index.js';

export default createComponent({
  factory() {
    return createHeadAndContent('<style>.async{color:red}</style>', render`<div class="async">Async content</div>`);
  },
  propagation: 'self',
});
