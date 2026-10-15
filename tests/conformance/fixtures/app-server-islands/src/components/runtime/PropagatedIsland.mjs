import { createComponent, render, renderComponent } from 'astro/runtime/server/index.js';
import HeadContent from './HeadContent.mjs';

// A server island that renders a component with head content: a plain component that has no `propagation` of its own.
export default createComponent((result) => render`${renderComponent(result, 'Content', HeadContent, {}, {})}`);
