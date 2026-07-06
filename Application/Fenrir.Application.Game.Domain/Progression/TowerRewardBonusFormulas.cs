using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     One tribe's current tower-derived reward bonuses for this tick -- all zero when that tribe holds no
///     built tower of a given reward type. <see cref="SilverRatio" /> and <see cref="XpRatio" /> are ratios
///     added to a base amount (e.g. 0.05 = +5%); <see cref="CpForPvmBonus" />/<see cref="CpForPvpBonus" /> are
///     flat Contribution-Point adds. See <see cref="TowerRewardBonusTable" />'s remarks for how these 4 values
///     are derived every tick, and <see cref="World.Zone" />'s tower-reward consumption hooks
///     (<see cref="World.Zone.GrantMonsterKillExperience" />, the CP-for-PvM/PvP hooks in <c>Zone.Combat.cs</c>)
///     for where each field is actually spent.
/// </summary>
public readonly record struct TowerTribeRewardBonus(
    float SilverRatio,
    int CpForPvmBonus,
    int CpForPvpBonus,
    float XpRatio)
{
    /// <summary>All-zero -- the value for a tribe holding no built tower at all, or before the first tick recomputes.</summary>
    public static readonly TowerTribeRewardBonus None = default;
}

/// <summary>
///     Per-built-level reward bonus tables -- legacy <c>SetTowerSilverBonus</c>/<c>SetTowerCPBonus</c>/
///     <c>SetTowerExpBonus</c> (<c>Server/ts25zone/S07_MyGame01.cpp:13362-13463</c>). Built level is 1-4:
///     <see cref="TowerWarState.DecodeLevel" /> returns the raw packed digit (2/4/6/8), so built level is that
///     value halved -- see <see cref="TowerRewardBonusTable" /> for where that conversion happens. Any level
///     outside 1-4 (including 0/not-built) yields an all-zero bonus from every method here.
/// </summary>
public static class TowerRewardBonusFormulas
{
    public static float SilverBonusRatio(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 0.05f,
            2 => 0.10f,
            3 => 0.15f,
            4 => 0.20f,
            _ => 0f
        };
    }

    /// <summary>
    ///     Caps at +2 from level 2 onward -- NOT monotonic in lockstep with <see cref="CpForPvpBonus" />, a
    ///     verified legacy quirk (level 4 yields +2 PvM and +2 PvP simultaneously, not +2 total).
    /// </summary>
    public static int CpForPvmBonus(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 1,
            2 => 2,
            3 => 2,
            4 => 2,
            _ => 0
        };
    }

    /// <summary>Only starts contributing at level 3, unlike <see cref="CpForPvmBonus" /> (live from level 1).</summary>
    public static int CpForPvpBonus(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 0,
            2 => 0,
            3 => 1,
            4 => 2,
            _ => 0
        };
    }

    public static float XpBonusRatio(int builtLevel)
    {
        return builtLevel switch
        {
            1 => 0.25f,
            2 => 0.50f,
            3 => 0.75f,
            4 => 1.00f,
            _ => 0f
        };
    }
}

/// <summary>
///     Recomputes the 4 per-tribe bonus sets from all 12 towers' packed state -- legacy's
///     <c>ResetTowerBonus</c> + <c>UpdateTowerBonus</c> (<c>S07_MyGame01.cpp:13416-13463</c>), run once per
///     tick by every <c>ts25zone.exe</c> process (see <see cref="TowerRewardBonusSystem" />).
/// </summary>
/// <remarks>
///     Group tribe is <c>towerIndex / <see cref="TowersPerTribe" /></c> -- fixed and structural (server/zone
///     number hardcodes which 3 of the 12 slots belong to which tribe, <c>S07_MyGame01.cpp:1341-1352</c>),
///     never derived from <see cref="TowerWarState.GetControllingTribe" />: only a player whose own tribe
///     already matches a slot's fixed group may ever build/upgrade it, so the two would always agree anyway.
///     <para>
///         Overwrite, not additive/max: within one tribe's own 3 slots, if more than one holds a built tower of
///         the <em>same</em> reward type (only possible transiently -- the global cross-tribe cap of 3 same-type
///         towers doesn't forbid two of them landing in the same tribe's own group), the highest slot-local
///         index (processed last: 0, then 1, then 2) silently overwrites whatever the earlier one set. There is
///         no summation and no max-of here, matching the legacy's own single assignment per category.
///     </para>
///     <para>
///         Reads <see cref="TowerWarState.GetPackedState" /> directly (Fenrir's own mirror of the legacy's
///         shared <c>mTowerInfo-&gt;mState1Tower[]</c>) with no additional phase gating: a tower mid-<c>Building</c>
///         (upgrade submitted, guardian not yet respawned) still reports its old pre-upgrade level, and a
///         <c>Sieged</c> tower (guardian just killed, destroy-cooldown running) still reports its last-built
///         level, because in both cases the packed state itself isn't touched until
///         <see cref="TowerWarState.CompleteUpgrade" />/<see cref="TowerWarState.CompleteDestruction" /> fire --
///         exactly the same "read whatever the shared value currently says" semantics the legacy's own
///         <c>UpdateTowerBonus</c> has, since it too just re-reads <c>mState1Tower[]</c> every tick without
///         consulting the separate siege sub-state machine.
///     </para>
/// </remarks>
public static class TowerRewardBonusTable
{
    public const int TribeCount = 4;
    public const int TowersPerTribe = 3;

    /// <param name="packedStateByTowerIndex">
    ///     Exactly <see cref="TowerWarState.TowerCount" /> (12) entries, index = tower index (0-11).
    /// </param>
    public static ImmutableArray<TowerTribeRewardBonus> Recompute(ReadOnlySpan<int> packedStateByTowerIndex)
    {
        var bonuses = new TowerTribeRewardBonus[TribeCount];

        for (var tribe = 0; tribe < TribeCount; tribe++)
        {
            var silver = 0f;
            var cpForPvm = 0;
            var cpForPvp = 0;
            var xp = 0f;

            for (var local = 0; local < TowersPerTribe; local++)
            {
                var towerIndex = tribe * TowersPerTribe + local;
                var packed = packedStateByTowerIndex[towerIndex];
                var builtLevel = TowerWarState.DecodeLevel(packed) / 2;
                if (builtLevel <= 0)
                    continue;

                switch (TowerWarState.DecodeType(packed))
                {
                    case 1:
                        silver = TowerRewardBonusFormulas.SilverBonusRatio(builtLevel);
                        break;
                    case 2:
                        cpForPvm = TowerRewardBonusFormulas.CpForPvmBonus(builtLevel);
                        cpForPvp = TowerRewardBonusFormulas.CpForPvpBonus(builtLevel);
                        break;
                    case 3:
                        xp = TowerRewardBonusFormulas.XpBonusRatio(builtLevel);
                        break;
                }
            }

            bonuses[tribe] = new TowerTribeRewardBonus(silver, cpForPvm, cpForPvp, xp);
        }

        return ImmutableArray.Create(bonuses);
    }
}
