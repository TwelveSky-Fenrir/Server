namespace Fenrir.Network.Transport;

public sealed class SocketAdmissionGate(int maxConcurrentSockets)
{
    private int _current;

    public int MaxConcurrentSockets { get; } = maxConcurrentSockets;

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
        Interlocked.Decrement(ref _current);
    }
}
