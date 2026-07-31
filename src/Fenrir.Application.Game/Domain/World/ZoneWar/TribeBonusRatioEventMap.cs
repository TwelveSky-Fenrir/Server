using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class TribeBonusRatioEventMap
{
    public const int EventCode = 301;

    private const int GeneralExperienceUpRatioFamilyBase = 21;
    private const int ItemDropUpRatioFamilyBase = 31;
    private const int ItemDropUpRatioForMyoungFamilyBase = 41;
    private const int KillOtherTribeAddValueFamilyBase = 51;

    private const float RatioScale = 0.1f;

    public static bool Apply(ZoneCenterSiegeState state, ReadOnlySpan<byte> data, ILogger? logger = null)
    {
        var eventSort = ReadInt32(data, 0);
        var eventValue = ReadInt32(data, 4);

        switch (eventSort)
        {
            case >= GeneralExperienceUpRatioFamilyBase
                and < GeneralExperienceUpRatioFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - GeneralExperienceUpRatioFamilyBase);
                state.SetExperienceBonusRatio(tribeIndex, eventValue * RatioScale);
                return true;
            }

            case >= ItemDropUpRatioFamilyBase and < ItemDropUpRatioFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - ItemDropUpRatioFamilyBase);
                state.SetItemDropBonusRatio(tribeIndex, eventValue * RatioScale);
                return true;
            }

            case >= ItemDropUpRatioForMyoungFamilyBase
                and < ItemDropUpRatioForMyoungFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - ItemDropUpRatioForMyoungFamilyBase);
                state.SetMyoungItemDropBonusRatio(tribeIndex, eventValue * RatioScale);
                return true;
            }

            case >= KillOtherTribeAddValueFamilyBase
                and < KillOtherTribeAddValueFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - KillOtherTribeAddValueFamilyBase);
                state.SetKillOtherTribeBonus(tribeIndex, eventValue);
                return true;
            }

            default:
                logger?.LogDebug(
                    "Tribe bonus-ratio event (tSort=301) carried unrecognized tEventSort {EventSort} -- ignored, matching legacy's own missing default case",
                    eventSort);
                return true;
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
}
