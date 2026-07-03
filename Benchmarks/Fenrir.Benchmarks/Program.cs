// Benchmarks gates (0 B/op) implémentés en Phase 8, contre les sérialiseurs générés
// et le FrameDecoder — voir Docs/Fenrir_Architecture_Reference_2026.md §14.4.

using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Fenrir.Benchmarks.Program).Assembly).Run(args);

namespace Fenrir.Benchmarks
{
    internal class Program;
}
