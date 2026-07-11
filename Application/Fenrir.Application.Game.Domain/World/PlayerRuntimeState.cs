using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Abstractions;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class PlayerRuntimeState
{
    public required int CharacterId { get; init; }
    public required IPacketSession Session { get; init; }
    public required string Name { get; init; }

        public required byte Tribe { get; set; }

    public required byte Gender { get; init; }
    public required byte HeadType { get; init; }
    public required byte FaceType { get; init; }

        public required short Level { get; set; }

    public short MapId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }

        public int StatVit { get; set; }

    public int StatStr { get; set; }
    public int StatInt { get; set; }
    public int StatDex { get; set; }

        public int StatPoints { get; set; }

    public int SkillPoints { get; set; }

        public ImmutableDictionary<byte, LearnedSkill> LearnedSkills { get; set; } =
        ImmutableDictionary<byte, LearnedSkill>.Empty;

        public long Experience { get; set; }

        public short Level2 { get; set; }

        public int Exp2 { get; set; }

        public int RebirthCount { get; set; }

        public int CombinedLevel => Level + Level2;

        public int Title { get; set; }

        public int Halo { get; set; }

        public int ContributionPoints { get; set; }

        public int TeacherPoint { get; set; }

        public int Zone241Time { get; set; }

        public int WarPoint { get; set; }

        public int BloodCoin { get; set; }

        public EffectiveStats? Stats { get; set; }

        public InventoryState Inventory { get; } = new();

        public SemaphoreSlim EconomyActionLock { get; } = new(1, 1);

        public DateTime LastItemUseUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastEnchantAttemptUtc { get; set; } = DateTime.UtcNow;

        public long FlushSequence { get; set; }

        public void MarkProgressDirty(DirtyTracker<int> dirtyTracker, DirtyFlags flags)
    {
        FlushSequence++;
        dirtyTracker.MarkDirty(CharacterId, flags);
    }
}
