import {
  createComponent,
  createHeadAndContent,
  render,
  renderComponent,
  renderSlotToString,
  renderUniqueStylesheet,
  unescapeHTML,
} from 'astro/runtime/server/index.js';

// The components that head-injection-app.test.ts writes inline with createComponent(): a component whose factory returns
// createHeadAndContent() and says it propagates its head ('self'), and the components above it ('in-tree'). Real pages import
// them (src/pages/head-injection), so the head that they propagate is injected by the pipeline of the host.

const Other = createComponent(() => render`<div id="other">Other</div>`);

export const HeadEntry = createComponent({
  factory(result: any, props: any, slots: any) {
    const link = renderUniqueStylesheet(result, {
      type: 'external',
      src: '/some/fake/styles.css',
    });
    return createHeadAndContent(
      unescapeHTML(link) as unknown as string,
      render`${renderComponent(result, 'Other', Other, props, slots)}`,
    );
  },
  propagation: 'self',
});

export const Content = createComponent(
  (result: any) => render`${renderComponent(result, 'HeadEntry', HeadEntry, {}, {})}`,
);
Content.propagation = 'in-tree';

export const Inner = createComponent(
  (result: any) => render`${renderComponent(result, 'Content', Content, {}, {})}`,
);
Inner.propagation = 'in-tree';

export const SlottedContent = createComponent({
  factory(result: any) {
    const link = renderUniqueStylesheet(result, {
      type: 'external',
      src: '/styles/from-slot.css',
    });
    return createHeadAndContent(unescapeHTML(link) as unknown as string, render`<p>Paragraph.</p>`);
  },
  propagation: 'self',
});

export const SlotRenderComponent = createComponent({
  async factory(result: any, _props: any, slots: any) {
    const html = await renderSlotToString(result, slots.default);
    const ownLink = renderUniqueStylesheet(result, {
      type: 'external',
      src: '/styles/slot-render.css',
    });
    return createHeadAndContent(ownLink!, render`<div class="p-sample">${unescapeHTML(html)}</div>`);
  },
  propagation: 'self',
});

// The component of the Content entry of a collection (propagation: 'self') that renders into a named slot (#16181).
const ContentInner = createComponent(() => render`<div class="box">styled content</div>`);

export const ContentEntry = createComponent({
  factory(result: any) {
    const style = '<style>.box{color:hotpink}</style>';
    return createHeadAndContent(
      unescapeHTML(style) as unknown as string,
      render`${renderComponent(result, 'ContentInner', ContentInner, {})}`,
    );
  },
  propagation: 'self',
});
