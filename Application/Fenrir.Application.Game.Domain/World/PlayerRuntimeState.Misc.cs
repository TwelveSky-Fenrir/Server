using System.Collections.Immutable;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    private static readonly ImmutableArray<(int ItemId, int Count)> DefaultBottleSlots =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)
    ];

        public int VisibleState { get; set; } = 1;

        public int SpecialState { get; set; }

        public bool UseOrnament { get; set; }

        public int ProtectForHalo { get; set; }

        public DateTime LastHaloEnchantAttemptUtc { get; set; } = DateTime.UtcNow;

        public int BonusItemLevel { get; set; }

        public bool BonusItemValue { get; set; }

        public int TribeNotifyScrollCount { get; set; }

        public byte PreviousTribe { get; set; }

        public int TribeFourReturnAllowance { get; set; }

        public ImmutableArray<(int ItemId, int Count)> BottleSlots { get; set; } = DefaultBottleSlots;

        public bool PshopOpen { get; set; }

        public PshopInfo? PshopListing { get; set; }

        public int FishingState { get; set; }

        public int FishingStep { get; set; }

        public DateTime? FishingCastAtUtc { get; set; }

        public bool CatchingFish { get; set; }
}
