namespace Fenrir.Application.Login.Hosting;

public sealed class LoginSocketAdmissionGate(int maxConcurrentConnections)
{
    private int _current;

    public int MaxConcurrentConnections { get; } = maxConcurrentConnections;

    public int Current => Volatile.Read(ref _current);

    public bool TryAcquire()
    {
        var observed = Volatile.Read(ref _current);

        while (observed < MaxConcurrentConnections)
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
