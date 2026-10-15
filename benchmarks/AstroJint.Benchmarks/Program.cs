using AstroJint.Benchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

// Every benchmark runs in this process: the engines are expensive to create and the sample site is the same for all of them.
//   dotnet run -c Release --project benchmarks/AstroJint.Benchmarks -- --filter "*Static*"
//   dotnet run -c Release --project benchmarks/AstroJint.Benchmarks -- --filter "*" --quick
// --quick runs a few short iterations: enough to see a change, not enough to publish a number.
var quick = args.Contains("--quick");
var job = (quick ? Job.ShortRun : Job.Default).WithToolchain(InProcessNoEmitToolchain.Instance);

var config = ManualConfig
    .Create(DefaultConfig.Instance)
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddJob(job);

BenchmarkSwitcher.FromAssembly(typeof(BenchmarkSite).Assembly).Run(args.Where(static a => a != "--quick").ToArray(), config);
