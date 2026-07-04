using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Fenrir.Benchmarks.Program).Assembly).Run(args);

namespace Fenrir.Benchmarks
{
    internal class Program;
}
