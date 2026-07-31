using System.Collections.Immutable;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    private static readonly ImmutableArray<(int SkillId, int Grade)> DefaultAutoBuffSkill =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)
    ];

    public bool AutoHuntEnabled { get; set; }

    public AutoHunt? AutoHuntConfig { get; set; }

    public byte AutoLifeRatio { get; set; }

    public byte AutoManaRatio { get; set; }

    public TimeSpan? LastHeroRankingPreviousQueryAtZoneClock { get; set; }

    public TimeSpan? LastHeroRankingCurrentQueryAtZoneClock { get; set; }

    public ImmutableArray<(int SkillId, int Grade)> AutoBuffSkill { get; set; } = DefaultAutoBuffSkill;

    public int AutoBuffTime { get; set; }

    public int StateTimeEffect { get; set; }

    public int RankBuffType { get; set; }
}
