namespace Fenrir.Domain.Login;

public sealed class LoginCapacityState
{
    private int _currentPlayers;
    private int _gagePlayers;
    private int _maxPlayers = -1;
    private int _reservedPlayers;

    public int MaxPlayers => Volatile.Read(ref _maxPlayers);

    public int GagePlayers => Volatile.Read(ref _gagePlayers);

    public int CurrentPlayers => Volatile.Read(ref _currentPlayers);

    public int ReservedPlayers => Volatile.Read(ref _reservedPlayers);

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

    public void ResetReservations()
    {
        Volatile.Write(ref _reservedPlayers, 0);
    }

    public LoginCapacityOutcome TryReserveSlot()
    {
        var reserved = Volatile.Read(ref _reservedPlayers);

        while (true)
        {
            var outcome = LoginCapacityGate.Evaluate(MaxPlayers, CurrentPlayers + reserved);
            if (outcome != LoginCapacityOutcome.Allowed)
                return outcome;

            var previous = Interlocked.CompareExchange(ref _reservedPlayers, reserved + 1, reserved);
            if (previous == reserved)
                return LoginCapacityOutcome.Allowed;

            reserved = previous;
        }
    }

    public void ReleaseReservedSlot()
    {
        Interlocked.Decrement(ref _reservedPlayers);
    }
}
