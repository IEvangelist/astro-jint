# Third-party notices

This repository is licensed under the MIT License (see [LICENSE](LICENSE)). It contains material from the project below,
and it depends on other packages that it does not redistribute.

## Astro

The conformance tests in `tests/AstroJint.Conformance.Tests` and the site fixtures in `tests/conformance` are ported or
adapted from the test suites of the official adapters and of the core package in
[withastro/astro](https://github.com/withastro/astro) (commit `71e2b24bb365e5a8809fc0432716f91ecc6d8ab5`), which is
released under the MIT License. A few binary assets of those fixtures (images and the font files `roboto-normal-400.woff2`
and `bitgeneva12.woff2`) are copied as they are and keep the terms that they have upstream.

The license file of Astro carries three notices: its own, and one each for the parts of `packages/astro` that come from
SvelteKit and from Vite. All three have the same MIT terms. They are repeated here because a ported file can contain
code from any of those parts:

```text
Copyright (c) 2021 Fred K. Schott
Copyright (c) 2020 [these people](https://github.com/sveltejs/kit/graphs/contributors)
Copyright (c) 2019-present, Yuxi (Evan) You and Vite contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Packages that are used but not copied

NuGet and npm install these; their license files travel with the packages, and nothing of them is in this repository.

| Package | Used for | License |
| --- | --- | --- |
| [Jint](https://github.com/sebastienros/jint) | The JavaScript engine that the host runs (a dependency of `AstroJint.AspNetCore`) | BSD-2-Clause |
| [Acornima](https://github.com/adams85/acornima) | The JavaScript parser of Jint | BSD-3-Clause |
| [AngleSharp](https://github.com/AngleSharp/AngleSharp) | Tests | MIT |
| [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) | Benchmarks | MIT |
| [xunit.v3](https://github.com/xunit/xunit) | Tests | Apache-2.0 |
| Microsoft.AspNetCore.TestHost and Microsoft.AspNetCore.Mvc.Testing | Tests | MIT |

An application that uses `AstroJint.AspNetCore` ships Jint and Acornima with it when it is published, so that application
should carry their notices too.
