using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>Which server-specific CP-bonus criterion (if any) a Regular War map applies at reward payout.</summary>
/// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5333-5350.</remarks>
public enum RegularWarCpBonusCriterion : byte
{
    None = 0,

    /// <summary>Server 120: restricted to characters at exactly rebirth tier 11 (<c>aLevel2</c>, the 1-12 post-cap ladder).</summary>
    RebirthTierExactly11 = 1,

    /// <summary>Server 164: restricted to characters with a rebirth count (<c>aRebirthNum</c>) of 0 through 6 inclusive.</summary>
    RebirthCount0To6 = 2
}

/// <summary>
///     One map's flat CP-bonus grant (server 120 or 164 only -- <see cref="RegularWarMapConfig.CpBonusRule" />
///     is <see langword="null" /> for every other configured map).
/// </summary>
/// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:5333-5350.</remarks>
public readonly record struct RegularWarCpBonusRule(
    int WinningSideAmount,
    int LosingSideAmount,
    RegularWarCpBonusCriterion Criterion)
{
    /// <summary>
    ///     <paramref name="rebirthTier" /> is the character's <c>aLevel2</c> (1-12 post-cap ladder, 0 = not yet
    ///     reached); <paramref name="rebirthCount" /> is the separate <c>aRebirthNum</c> counter (0-6). The
    ///     contract text names these "rebirth tier" and "rebirth count" respectively -- two distinct fields, not
    ///     interchangeable.
    /// </summary>
    public bool IsSatisfiedBy(short rebirthTier, int rebirthCount)
    {
        return Criterion switch
        {
            RegularWarCpBonusCriterion.RebirthTierExactly11 => rebirthTier == 11,
            RegularWarCpBonusCriterion.RebirthCount0To6 => rebirthCount is >= 0 and <= 6,
            _ => false
        };
    }
}

/// <summary>One of the 11 map instances configured to run the Regular War (Zone049) schedule.</summary>
/// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:647-698 (<c>MyGame::Init</c>'s one-time assignment).</remarks>
public readonly record struct RegularWarMapConfig(
    short MapId,
    byte WarSlotIndex,
    bool IsBossWar,
    bool AnnouncesSmallestPresentTribe,
    RegularWarCpBonusRule? CpBonusRule);

/// <summary>
///     The fixed set of 11 map instances (identified by legacy server number) that run the Regular War
///     schedule, each bound to a war-slot index 0-10 out of the legacy's 13-slot shared-state array.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:647-698 (server numbers, slot assignment, boss-war flag,
///         cooldown pre-seed) ; Server/Header/Protocol/STRUCT.h:577,607-608 (13-slot shared-state array sizing).
///     </para>
///     <para>
///         <see cref="RegularWarMapConfig.WarSlotIndex" /> is assigned in the order the contract's own
///         precondition list enumerates these servers (49, 146, 149, 154, 157, 160, 120, 121, 122, 295, 164).
///         The legacy's shared 13-slot array is externally owned (see <see cref="RegularWarSchedule" />'s own
///         remarks for why Fenrir does not reproduce it); each map's schedule is instead owned entirely by
///         whichever shard hosts that map, so the slot index has no behavioral consumer in this cluster -- it
///         is retained purely for citation fidelity and log correlation.
///     </para>
/// </remarks>
public static class RegularWarMapCatalog
{
    public static readonly ImmutableArray<RegularWarMapConfig> ConfiguredMaps = BuildConfiguredMaps();

    public static bool TryGet(short mapId, out RegularWarMapConfig config)
    {
        foreach (var candidate in ConfiguredMaps)
            if (candidate.MapId == mapId)
            {
                config = candidate;
                return true;
            }

        config = default;
        return false;
    }

    private static ImmutableArray<RegularWarMapConfig> BuildConfiguredMaps()
    {
        ReadOnlySpan<short> servers = [49, 146, 149, 154, 157, 160, 120, 121, 122, 295, 164];

        var builder = ImmutableArray.CreateBuilder<RegularWarMapConfig>(servers.Length);
        for (byte slot = 0; slot < servers.Length; slot++)
        {
            var mapId = servers[slot];

            RegularWarCpBonusRule? cpBonus = mapId switch
            {
                120 => new RegularWarCpBonusRule(50, 25, RegularWarCpBonusCriterion.RebirthTierExactly11),
                164 => new RegularWarCpBonusRule(100, 50, RegularWarCpBonusCriterion.RebirthCount0To6),
                _ => null
            };

            builder.Add(new RegularWarMapConfig(
                mapId,
                slot,
                mapId == 295,
                mapId == 160,
                cpBonus));
        }

        return builder.MoveToImmutable();
    }
}
