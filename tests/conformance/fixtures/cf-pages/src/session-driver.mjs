// The session driver of a user, for the tests of session-false: an unstorage driver that keeps the data in memory and tells
// what it does, which the test reads from the log. Every engine of the host has a store of its own, so the tests do not
// count on the data that it holds.
export default function conformanceSessionDriver() {
  const store = new Map();
  return {
    name: 'conformance-driver',
    options: {},
    async getItem(key) {
      console.log(`conformance-driver getItem ${key}`);
      return store.get(key) ?? null;
    },
    async setItem(key, value) {
      console.log(`conformance-driver setItem ${key}`);
      store.set(key, value);
    },
    async removeItem(key) {
      console.log(`conformance-driver removeItem ${key}`);
      store.delete(key);
    },
  };
}
