namespace Fenrir.Domain.Login;

public enum LoginCapacityOutcome
{
    Allowed,
    Maintenance,
    ServerFull
}

public static class LoginCapacityGate
{
    public static LoginCapacityOutcome Evaluate(int maxPlayers, int currentPlayers)
    {
        if (maxPlayers == 0)
            return LoginCapacityOutcome.Maintenance;

        return currentPlayers >= maxPlayers ? LoginCapacityOutcome.ServerFull : LoginCapacityOutcome.Allowed;
    }
}
