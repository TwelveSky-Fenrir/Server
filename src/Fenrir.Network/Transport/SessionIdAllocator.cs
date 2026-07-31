namespace Fenrir.Network.Transport;

public sealed class SessionIdAllocator
{
    private long _lastAllocated;
    public static SessionIdAllocator Shared { get; } = new();

    public long Next()
    {
        return Interlocked.Increment(ref _lastAllocated);
    }
}
