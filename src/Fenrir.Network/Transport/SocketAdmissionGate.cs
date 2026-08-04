namespace Fenrir.Network.Transport;

public sealed class SocketAdmissionGate
{
    private int _current;

    public SocketAdmissionGate(int maxConcurrentSockets)
    {
        if (maxConcurrentSockets <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentSockets));

        MaxConcurrentSockets = maxConcurrentSockets;
    }

    public int MaxConcurrentSockets { get; }

    public int Current => Volatile.Read(ref _current);

    public bool TryAcquire()
    {
        var observed = Volatile.Read(ref _current);

        while (observed < MaxConcurrentSockets)
        {
            var previous = Interlocked.CompareExchange(ref _current, observed + 1, observed);
            if (previous == observed)
                return true;

            observed = previous;
        }

        return false;
    }

    public void Release()
    {
        var observed = Volatile.Read(ref _current);

        while (observed > 0)
        {
            var previous = Interlocked.CompareExchange(ref _current, observed - 1, observed);
            if (previous == observed)
                return;

            observed = previous;
        }
    }
}
