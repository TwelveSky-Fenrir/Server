namespace Fenrir.Domain.Login.Avatars;

public static class TribeLineageGate
{
    public static bool IsCoherent(byte tribe, byte previousTribe)
    {
        return tribe switch
        {
            0 or 1 or 2 => previousTribe == tribe,
            FourthFactionGate.FourthFactionTribe => previousTribe is 0 or 1 or 2,
            _ => false
        };
    }

    public static bool BlocksCreation(byte tribe, byte previousTribe)
    {
        return !IsCoherent(tribe, previousTribe);
    }
}
