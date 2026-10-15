import { createComponent, createHeadAndContent, maybeRenderHead, render } from 'astro/runtime/server/index.js';

// A component that hands head content (a style, a stylesheet link, a script) to whoever renders it, the way the render of a content
// collection entry does. `propagation: 'self'` is what makes the runtime collect that head content.
export default createComponent({
  factory() {
    return createHeadAndContent(
      '<style>.box{color:red}</style><link rel="stylesheet" href="/content.css"><script type="module" src="/content.js"></script>',
      render`${maybeRenderHead()}<div class="box">Island content</div>`,
    );
  },
  propagation: 'self',
});
