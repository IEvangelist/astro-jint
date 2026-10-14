using Jint;

var engine = new Engine(options => options.UseWebApis());

foreach (var name in new[] { "Request", "Response", "Headers", "URL", "ReadableStream", "TextEncoder", "AbortController", "crypto", "queueMicrotask", "setTimeout", "process", "AsyncLocalStorage" })
{
    Console.WriteLine($"{name}: {engine.Evaluate($"typeof {name}")}");
}
