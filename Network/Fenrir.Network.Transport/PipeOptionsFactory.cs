using System.Buffers;
using System.IO.Pipelines;

namespace Fenrir.Network.Transport;

public static class PipeOptionsFactory
{
    public static PipeOptions Rx { get; } = new(
        MemoryPool<byte>.Shared,
        PipeScheduler.ThreadPool,
        PipeScheduler.Inline,
        512 * 1024,
        256 * 1024,
        4096,
        false);

    public static PipeOptions Tx { get; } = new(
        MemoryPool<byte>.Shared,
        PipeScheduler.ThreadPool,
        PipeScheduler.Inline,
        128 * 1024,
        64 * 1024,
        4096,
        false);
}
