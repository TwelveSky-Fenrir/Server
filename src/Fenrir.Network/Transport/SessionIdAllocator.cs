namespace Fenrir.Network.Transport;

public sealed class SessionIdAllocator
{
    public static SessionIdAllocator Shared { get; } = new();

    private long _lastAllocated;

    public long Next()
    {
        return Interlocked.Increment(ref _lastAllocated);
    }
}
