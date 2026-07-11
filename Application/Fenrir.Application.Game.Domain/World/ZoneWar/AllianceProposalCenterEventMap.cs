using System.Buffers.Binary;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class AllianceProposalCenterEventMap
{
    public const int FinalizeNewAllianceEventCode = 46;

    public const int BreakAllianceViaRitualEventCode = 47;

    public const int AllianceStoneUnderAttackEventCode = 48;

    public const int BreakAllianceViaStoneCaptureEventCode = 49;

    public const int EventCodeRangeStart = FinalizeNewAllianceEventCode;

    public const int EventCodeRangeEnd = BreakAllianceViaStoneCaptureEventCode;

    public static void Apply(int eventCode, ReadOnlySpan<byte> data, AllianceProposalCenterState state,
        ILogger logger)
    {
        switch (eventCode)
        {
            case FinalizeNewAllianceEventCode:
                ApplyFinalizeNewAlliance(data, state, logger);
                break;

            case BreakAllianceViaRitualEventCode:
            case BreakAllianceViaStoneCaptureEventCode:
                ApplyBreakAlliance(eventCode, data, state, logger);
                break;

            case AllianceStoneUnderAttackEventCode:
                break;
        }
    }

    private static void ApplyFinalizeNewAlliance(ReadOnlySpan<byte> data, AllianceProposalCenterState state,
        ILogger logger)
    {
        var tribeA = ReadInt32(data, 0);
        var tribeB = ReadInt32(data, 4);

        if (!TryValidateTribes(tribeA, tribeB, FinalizeNewAllianceEventCode, logger))
            return;

        state.ApplyFinalize((byte)tribeA, (byte)tribeB);
    }

    private static void ApplyBreakAlliance(int eventCode, ReadOnlySpan<byte> data, AllianceProposalCenterState state,
        ILogger logger)
    {
        var tribeA = ReadInt32(data, 0);
        var tribeB = ReadInt32(data, 4);
        var expiryA = ReadInt32(data, 8);
        var expiryB = ReadInt32(data, 12);

        if (!TryValidateTribes(tribeA, tribeB, eventCode, logger))
            return;

        state.ApplyBreak((byte)tribeA, (byte)tribeB, expiryA, expiryB);
    }

    private static bool TryValidateTribes(int tribeA, int tribeB, int eventCode, ILogger logger)
    {
        if (AllianceProposalCenterState.IsValidTribe(tribeA) && AllianceProposalCenterState.IsValidTribe(tribeB))
            return true;

        logger.LogWarning(
            "Alliance proposal event {EventCode} referenced out-of-range tribe id(s) {TribeA}/{TribeB} -- ignored",
            eventCode, tribeA, tribeB);
        return false;
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
}
