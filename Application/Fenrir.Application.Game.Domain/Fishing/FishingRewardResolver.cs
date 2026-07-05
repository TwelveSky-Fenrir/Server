using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Fishing;

/// <summary>
///     RNG resolution shared by CZ_FISHING_RESULT_SEND Sort=1 (poll bite) and CZ_FISHING_REWARD_SEND. Item ids
///     are the raw <c>EFISH_*</c> catch tags themselves (STRUCT.h:1800: 582 Elixir-Koi, 583 Mount-Koi, 584
///     Pet-Koi, 586 Ticket-Koi) -- S04_MyWork02.cpp:13920-13933 grants the koi item as-is, it does not resolve
///     it into the mount/pet/elixir/ticket it represents (that only happens later, when the koi is used via
///     CZ_USE_INVENTORY_ITEM_SEND, out of this batch's scope).
/// </summary>
public static class FishingRewardResolver
{
    private const int ElixirKoiItemId = 582;
    private const int MountKoiItemId = 583;
    private const int PetKoiItemId = 584;
    private const int TicketKoiItemId = 586;

    /// <summary>10% chance of a bite (step 4); 90% a miss (step 5).</summary>
    public static bool RollBite(IRandomSource random)
    {
        return random.NextInt32(10) == 0;
    }

    /// <summary>1/15 mount-or-pet koi (50/50 split), 3/15 elixir koi, 11/15 ticket koi.</summary>
    public static int RollRewardItem(IRandomSource random)
    {
        var roll = random.NextInt32(15);
        if (roll == 0)
            return random.NextInt32(2) == 0 ? MountKoiItemId : PetKoiItemId;

        return roll <= 3 ? ElixirKoiItemId : TicketKoiItemId;
    }
}
