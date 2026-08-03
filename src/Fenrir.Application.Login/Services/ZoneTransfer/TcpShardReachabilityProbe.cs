using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Domain.Login;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.ZoneTransfer;

public sealed class TcpShardReachabilityProbe(
    IOptions<LoginServerOptions> options,
    ILogger<TcpShardReachabilityProbe> logger) : IShardReachabilityProbe
{
    private static readonly long ReachableTtlTimestamps = Stopwatch.Frequency * 2;
    private static readonly long UnreachableTtlTimestamps = Stopwatch.Frequency / 2;

    private readonly ConcurrentDictionary<(string Host, int Port), CachedProbe> _cache = new();

    public async ValueTask<bool> IsReachableAsync(string host, int port, CancellationToken ct)
    {
        var key = (host, port);

        while (true)
        {
            _ = _cache.TryGetValue(key, out var existing);

            if (existing is not null && !existing.HasExpired(Stopwatch.GetTimestamp()))
                return await existing.Completion.WaitAsync(ct).ConfigureAwait(false);

            var fresh = new CachedProbe();

            var installed = existing is null
                ? _cache.TryAdd(key, fresh)
                : _cache.TryUpdate(key, fresh, existing);

            if (!installed)
                continue;

            _ = RunProbeAsync(host, port, fresh);

            return await fresh.Completion.WaitAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task RunProbeAsync(string host, int port, CachedProbe entry)
    {
        try
        {
            var reachable = await ConnectAsync(host, port).ConfigureAwait(false);

            entry.Complete(reachable,
                Stopwatch.GetTimestamp() + (reachable ? ReachableTtlTimestamps : UnreachableTtlTimestamps));
        }
        catch (Exception ex)
        {
            entry.Fail(ex);
        }
    }

    private async Task<bool> ConnectAsync(string host, int port)
    {
        var timeoutMilliseconds = options.Value.ShardReachabilityProbeTimeoutMilliseconds;
        using var client = new TcpClient();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMilliseconds));

        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (SocketException ex)
        {
            logger.LogDebug(ex, "Shard reachability probe: TCP connect to {Host}:{Port} failed", host, port);
            return false;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug(
                "Shard reachability probe: TCP connect to {Host}:{Port} timed out after {TimeoutMs}ms", host,
                port, timeoutMilliseconds);
            return false;
        }
    }

    private sealed class CachedProbe
    {
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private long _expiresAtTimestamp = long.MaxValue;

        public Task<bool> Completion => _completion.Task;

        public bool HasExpired(long nowTimestamp)
        {
            return nowTimestamp >= Volatile.Read(ref _expiresAtTimestamp);
        }

        public void Complete(bool reachable, long expiresAtTimestamp)
        {
            Volatile.Write(ref _expiresAtTimestamp, expiresAtTimestamp);
            _completion.TrySetResult(reachable);
        }

        public void Fail(Exception exception)
        {
            Volatile.Write(ref _expiresAtTimestamp, long.MinValue);
            _completion.TrySetException(exception);
        }
    }
}
