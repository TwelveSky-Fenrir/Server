using System.Buffers;
using System.IO.Pipelines;

namespace Fenrir.Network.Transport;

// RX/TX thresholds intentionally differ: TX is a quarter of RX's, to detect a slow-consuming client (§8.5) early.
public static class PipeOptionsFactory
{
    public static PipeOptions Rx { get; } = new(
        MemoryPool<byte>.Shared,
        PipeScheduler.ThreadPool,
        PipeScheduler.Inline, // the receive-loop is the only writer and already runs off the ThreadPool
        512 * 1024,
        256 * 1024,
        4096,
        false);

    public static PipeOptions Tx { get; } = new(
        MemoryPool<byte>.Shared,
        PipeScheduler.ThreadPool,
        PipeScheduler.Inline, // ClientSession.Send writes synchronously from arbitrary caller threads
        128 * 1024,
        64 * 1024,
        4096,
        false);
}
