using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Fishing;

public static class FishingRewardResolver
{
    private const int ElixirKoiItemId = 582;
    private const int MountKoiItemId = 583;
    private const int PetKoiItemId = 584;
    private const int TicketKoiItemId = 586;

    public static bool RollBite(IRandomSource random)
    {
        return random.NextInt32(10) == 0;
    }

    public static int RollRewardItem(IRandomSource random)
    {
        var roll = random.NextInt32(15);
        if (roll == 0)
            return random.NextInt32(2) == 0 ? MountKoiItemId : PetKoiItemId;

        return roll <= 3 ? ElixirKoiItemId : TicketKoiItemId;
    }
}
