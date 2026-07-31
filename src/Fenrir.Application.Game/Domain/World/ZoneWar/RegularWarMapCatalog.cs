using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum RegularWarCpBonusCriterion : byte
{
    None = 0,

    RebirthTierExactly11 = 1,

    RebirthCount0To6 = 2
}

public readonly record struct RegularWarCpBonusRule(
    int WinningSideAmount,
    int LosingSideAmount,
    RegularWarCpBonusCriterion Criterion)
{
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

public readonly record struct RegularWarMapConfig(
    short MapId,
    byte WarSlotIndex,
    bool IsBossWar,
    bool AnnouncesSmallestPresentTribe,
    RegularWarCpBonusRule? CpBonusRule);

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
        // Slot 10 est la carte 296, pas 164 : Server/ts25zone/S07_MyGame01.cpp:681-683 fait
        // "case 296: mZone049TypeZoneIndex = 10;" et 164 n'apparait nulle part dans ce switch.
        ReadOnlySpan<short> servers = [49, 146, 149, 154, 157, 160, 120, 121, 122, 295, 296];

        var builder = ImmutableArray.CreateBuilder<RegularWarMapConfig>(servers.Length);
        for (byte slot = 0; slot < servers.Length; slot++)
        {
            var mapId = servers[slot];

            RegularWarCpBonusRule? cpBonus = mapId switch
            {
                120 => new RegularWarCpBonusRule(50, 25, RegularWarCpBonusCriterion.RebirthTierExactly11),
                // Ce bras devient INATTEIGNABLE avec la correction ci-dessus : 164 n'est pas une carte de
                // regular war. La carte reellement visee reste a etablir contre le legacy avant de le
                // reaffecter -- un bonus de CP attribue a la mauvaise carte est silencieux.
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
