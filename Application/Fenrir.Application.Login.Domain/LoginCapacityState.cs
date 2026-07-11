namespace Fenrir.Application.Login.Domain;

public sealed class LoginCapacityState
{
    private int _currentPlayers;
    private int _maxPlayers = -1;

        public int MaxPlayers => Volatile.Read(ref _maxPlayers);

    public int CurrentPlayers => Volatile.Read(ref _currentPlayers);

    public void SetMaxPlayers(int value)
    {
        Volatile.Write(ref _maxPlayers, value);
    }

    public void SetCurrentPlayers(int value)
    {
        Volatile.Write(ref _currentPlayers, value);
    }
}
