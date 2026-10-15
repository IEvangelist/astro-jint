import {
  createKey,
  decodeKey,
  decryptString,
  encodeKey,
  encryptString,
  generateCspDigest,
} from '../../../node_modules/astro/dist/core/encryption.js';

// The calls of encryption.test.ts, with its arguments. `astro` does not export core/encryption.js, so it is imported by its path
// inside the installed package; the bundle of the host then runs the very file that encrypts the props, the slots and the export of
// every server island (WebCrypto: AES-GCM and SHA-2 of the interpreter). A case answers what its test asserts on, and `true` for
// "the call threw".

async function throws(call: () => Promise<unknown>): Promise<boolean> {
  try {
    await call();
    return false;
  } catch {
    return true;
  }
}

export const cases: Record<string, () => unknown> = {
  // encryptString / decryptString
  'round-trips correctly': async () => {
    const key = await createKey();
    const original = 'hello world';
    const encrypted = await encryptString(key, original, 'props:TestComponent');
    return { original, decrypted: await decryptString(key, encrypted, 'props:TestComponent') };
  },
  'round-trips an empty string': async () => {
    const key = await createKey();
    const encrypted = await encryptString(key, '', 'props:TestComponent');
    return { decrypted: await decryptString(key, encrypted, 'props:TestComponent') };
  },
  'round-trips a JSON payload': async () => {
    const key = await createKey();
    const original = JSON.stringify({ foo: 'bar', num: 42, nested: { a: [1, 2] } });
    const encrypted = await encryptString(key, original, 'props:TestComponent');
    return { original, decrypted: await decryptString(key, encrypted, 'props:TestComponent') };
  },
  'produces a different ciphertext on each call (IV randomness)': async () => {
    const key = await createKey();
    const plain = 'same input';
    const first = await encryptString(key, plain, 'props:TestComponent');
    const second = await encryptString(key, plain, 'props:TestComponent');
    return { first, second };
  },
  'both distinct ciphertexts decrypt to the same plaintext': async () => {
    const key = await createKey();
    const plain = 'same input';
    const first = await encryptString(key, plain, 'props:TestComponent');
    const second = await encryptString(key, plain, 'props:TestComponent');
    return {
      plain,
      distinct: first !== second,
      first: await decryptString(key, first, 'props:TestComponent'),
      second: await decryptString(key, second, 'props:TestComponent'),
    };
  },
  'throws when decrypting a tampered ciphertext': async () => {
    const key = await createKey();
    const encrypted = await encryptString(key, 'secret', 'props:TestComponent');
    // Flip the last character to corrupt the ciphertext
    const tampered = encrypted.slice(0, -1) + (encrypted.endsWith('A') ? 'B' : 'A');
    return throws(() => decryptString(key, tampered, 'props:TestComponent'));
  },
  'throws when decrypting with the wrong key': async () => {
    const keyA = await createKey();
    const keyB = await createKey();
    const encrypted = await encryptString(keyA, 'secret', 'props:TestComponent');
    return throws(() => decryptString(keyB, encrypted, 'props:TestComponent'));
  },
  'throws when decrypting with mismatched additionalData': async () => {
    const key = await createKey();
    // Encrypt props for ComponentA, try to decrypt as slots for ComponentB
    const encrypted = await encryptString(key, '{"post":"hello"}', 'props:ComponentA');
    return throws(() => decryptString(key, encrypted, 'slots:ComponentB'));
  },

  // encodeKey / decodeKey
  'round-trips a CryptoKey through base64': async () => {
    const key = await createKey();
    const encoded = await encodeKey(key);
    const decoded = await decodeKey(encoded);
    // Verify the decoded key works for encrypt/decrypt
    const plain = 'verify key works';
    const aad = 'props:TestComponent';
    const encrypted = await encryptString(decoded, plain, aad);
    return { plain, decrypted: await decryptString(decoded, encrypted, aad) };
  },
  'produces a string from encodeKey': async () => {
    const key = await createKey();
    const encoded = await encodeKey(key);
    return { type: typeof encoded, encoded };
  },
  'a key encoded then decoded can decrypt ciphertexts made with the original key': async () => {
    const key = await createKey();
    const plain = 'cross-key decrypt';
    const aad = 'props:TestComponent';
    const encrypted = await encryptString(key, plain, aad);
    const encoded = await encodeKey(key);
    const decoded = await decodeKey(encoded);
    return { plain, decrypted: await decryptString(decoded, encrypted, aad) };
  },

  // generateCspDigest
  'produces a sha256- prefixed base64 hash for SHA-256 algorithm': () => generateCspDigest('alert(1)', 'SHA-256'),
  'produces a sha384- prefixed hash for SHA-384 algorithm': () => generateCspDigest('alert(1)', 'SHA-384'),
  'produces a sha512- prefixed hash for SHA-512 algorithm': () => generateCspDigest('alert(1)', 'SHA-512'),
  'produces a deterministic hash for the same input': async () => ({
    a: await generateCspDigest('hello', 'SHA-256'),
    b: await generateCspDigest('hello', 'SHA-256'),
  }),
  'produces different hashes for different inputs': async () => ({
    a: await generateCspDigest('hello', 'SHA-256'),
    b: await generateCspDigest('world', 'SHA-256'),
  }),
};
