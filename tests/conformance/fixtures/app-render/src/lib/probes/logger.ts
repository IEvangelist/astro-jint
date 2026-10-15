import { createConsoleLogger } from 'astro/logger/console';
import jsonLoggerDestination, { createJsonLoggerFromFlags } from 'astro/logger/json';

// The calls of logger.test.ts on a logger of the runtime, run inside the host. The app of the unit tests (`app.logger`,
// `app.adapterLogger`) cannot be reached from a page, so the same classes are used on their own: the logger that
// `createConsoleLogger()` gives is what the app starts with and `createJsonLoggerFromFlags()` has the destination of the manifest.

export const cases: Record<string, () => unknown> = {
  // The adapterLogger is made from the options object of the logger, so that resolving the destination in place (setDestination)
  // must change what it writes through without making another options object.
  'adapter-logger-is-stable': () => {
    const logger = createConsoleLogger({ level: 'info' });
    const adapterLogger = logger.forkIntegrationLogger('adapter');
    const beforeOptions = adapterLogger.options;
    const destinationBefore = logger.options.destination;

    const events: Array<{ label: string | null; level: string; message: string }> = [];
    logger.setDestination({ write: (event: { label: string | null; level: string; message: string }) => events.push(event) });
    adapterLogger.info('through the swapped destination');

    return {
      destinationSwapped: logger.options.destination !== destinationBefore,
      loggerKeepsItsOptions: logger.options === beforeOptions,
      adapterLoggerKeepsItsOptions: adapterLogger.options === beforeOptions,
      events: events.map(({ label, level, message }) => ({ label, level, message })),
    };
  },

  'destination-of-the-manifest': () => {
    const destination = jsonLoggerDestination();
    return { write: typeof destination.write, flush: typeof (destination as any).flush, close: typeof (destination as any).close };
  },

  'flush-without-a-flush-method': () => {
    const logger = createJsonLoggerFromFlags({});
    logger.flush();
    return { destinationHasFlush: 'flush' in logger.options.destination };
  },

  'close-without-a-close-method': () => {
    const logger = createJsonLoggerFromFlags({});
    logger.close();
    return { destinationHasClose: 'close' in logger.options.destination };
  },
};
