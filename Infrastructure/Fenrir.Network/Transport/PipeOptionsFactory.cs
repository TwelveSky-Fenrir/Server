using System.Buffers;
using System.IO.Pipelines;

namespace Fenrir.Network.Transport;

/// <summary>
///     Centralizes the <see cref="PipeOptions" /> used by every <see cref="SocketConnection" /> so backpressure
///     tuning lives in one place. RX and TX intentionally differ: TX pauses/resumes at a quarter of RX's
///     thresholds because it exists to protect the server from a slow-consuming client — a client that stops
///     draining its socket must be detected (and evicted, §8.5) long before RX would ever notice anything wrong.
/// </summary>
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
