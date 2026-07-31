namespace Fenrir.Domain.Login.Avatars;

public static class FourthFactionGate
{
    public const byte FourthFactionTribe = TribeDominanceGate.TribeSlotCount - 1;

    public static bool BlocksCreation(byte tribe)
    {
        return tribe == FourthFactionTribe;
    }
}
