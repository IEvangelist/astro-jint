import { baseService } from 'astro/assets';

// The stub of upstream (test-image-service.ts): the pictures are returned as they are, so only the files that the build
// keeps or deletes matter and it does not need sharp.
export default {
  ...baseService,
  async transform(buffer, transform) {
    return { data: buffer, format: transform.format };
  },
};
