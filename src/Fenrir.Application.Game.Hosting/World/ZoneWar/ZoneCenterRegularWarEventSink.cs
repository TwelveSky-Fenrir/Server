using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class ZoneCenterRegularWarEventSink(
    ZoneCenterBroadcastIngestor ingestor,
    ILogger<ZoneCenterRegularWarEventSink> logger) : IRegularWarEventSink
{
    private const int CountdownAnnounceSubCode = 1;

    private const int CountdownFinishedSubCode = 2;

    private const int GateOpenedSubCode = 3;

    private const int ActiveWarStartedSubCode = 4;

    private const int AbortedEmptyMapSubCode = 5;

    private const int DrawSubCode = 6;

    private const int TribeWinSubCode = 7;

    private const int ReturnToTownSubCode = 8;

    private const int WarEndedToIdleSubCode = 9;

    public void OnCountdownAnnounced(short mapId, int remainingMinutes)
    {
        IngestForSlot(mapId, CountdownAnnounceSubCode, remainingMinutes);
    }

    public void OnCountdownFinished(short mapId)
    {
        IngestForSlot(mapId, CountdownFinishedSubCode);
    }

    public void OnGateOpened(short mapId)
    {
        IngestForSlot(mapId, GateOpenedSubCode);
    }

    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
    }

    public void OnActiveWarStarted(short mapId, int durationLegacyTicks)
    {
        IngestForSlot(mapId, ActiveWarStartedSubCode, durationLegacyTicks);
    }

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        switch (outcome)
        {
            case RegularWarOutcome.AbortedEmptyMap:
                IngestForSlot(mapId, AbortedEmptyMapSubCode);
                return;

            case RegularWarOutcome.TribeWin when winningTribe is { } winner:
                IngestForSlot(mapId, TribeWinSubCode, winner);
                return;

            default:
                IngestForSlot(mapId, DrawSubCode);
                return;
        }
    }

    public void OnReturnToTownAnnounced(short mapId)
    {
        IngestForSlot(mapId, ReturnToTownSubCode);
    }

    public void OnMonstersShouldDespawn(short mapId)
    {
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
        IngestForSlot(mapId, WarEndedToIdleSubCode);
    }

    private void IngestForSlot(short mapId, int subCode, int? secondField = null)
    {
        if (!TryResolveSlot(mapId, out var slot))
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        WriteInt32(payload, 0, slot);

        if (secondField is { } value)
            WriteInt32(payload, 4, value);

        ingestor.Ingest(subCode, payload);
    }

    private bool TryResolveSlot(short mapId, out int slot)
    {
        if (RegularWarMapCatalog.TryGet(mapId, out var config))
        {
            slot = config.WarSlotIndex;
            return true;
        }

        logger.LogWarning("RegularWar event for map {MapId} has no Zone049 slot in the catalog -- ignored", mapId);
        slot = 0;
        return false;
    }

    private static void WriteInt32(Span<byte> data, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(data[offset..], value);
    }
}
