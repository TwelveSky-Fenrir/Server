namespace Fenrir.Application.Login.Hosting;

// Server/ts25login/S02_MyServer.cpp:269-280 : balayage des mServerMaxUserNum slots a l'accept, closesocket
// immediat si aucun n'est libre, AVANT le moindre octet lu ou ecrit -- ni greeting, ni reponse.
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
