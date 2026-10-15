import { baseService } from 'astro/assets';

// The stub of upstream (test-image-service.ts): the pictures are returned as they are, so only the URLs that the pages
// write for them matter and the build does not need sharp.
export default {
  ...baseService,
  async transform(buffer, transform) {
    return { data: buffer, format: transform.format };
  },
};
