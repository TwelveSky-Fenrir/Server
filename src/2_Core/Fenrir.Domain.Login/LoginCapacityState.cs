namespace Fenrir.Domain.Login;

public sealed class LoginCapacityState
{
    private int _currentPlayers;
    private int _maxPlayers = -1;
    private int _gagePlayers;

    public int MaxPlayers => Volatile.Read(ref _maxPlayers);

    public int GagePlayers => Volatile.Read(ref _gagePlayers);

    public int CurrentPlayers => Volatile.Read(ref _currentPlayers);

    public void SetMaxPlayers(int value)
    {
        Volatile.Write(ref _maxPlayers, value);
    }

    public void SetGagePlayers(int value)
    {
        Volatile.Write(ref _gagePlayers, value);
    }

    public void SetCurrentPlayers(int value)
    {
        Volatile.Write(ref _currentPlayers, value);
    }
}
