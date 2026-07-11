namespace Fenrir.Application.Login.Domain.Avatars;

public static class FourthFactionGate
{
    public const byte FourthFactionTribe = TribeDominanceGate.TribeSlotCount - 1;

    public static bool BlocksCreation(byte tribe, bool enableFourthFaction)
    {
        return tribe == FourthFactionTribe && !enableFourthFaction;
    }
}
