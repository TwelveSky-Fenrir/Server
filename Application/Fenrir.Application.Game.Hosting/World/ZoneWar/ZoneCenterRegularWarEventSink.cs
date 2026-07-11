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

    private const int ActiveWarStartedSubCode = 4;

    private const int WarConcludedSubCode = 6;

    private const int WarEndedToIdleSubCode = 9;

    public void OnCountdownAnnounced(short mapId, int remainingMinutes)
    {
        if (!TryResolveSlot(mapId, out var slot))
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        WriteInt32(payload, 0, slot);

        WriteInt32(payload, 4, remainingMinutes);

        ingestor.Ingest(CountdownAnnounceSubCode, payload);
    }

    public void OnSmallestTribeFlagged(short mapId, byte tribeId)
    {
    }

    public void OnActiveWarStarted(short mapId)
    {
        IngestForSlot(mapId, ActiveWarStartedSubCode);
    }

    public void OnWarConcluded(short mapId, RegularWarOutcome outcome, byte? winningTribe,
        ImmutableArray<RegularWarRewardGrant> rewards, bool bossMonstersShouldSpawn)
    {
        IngestForSlot(mapId, WarConcludedSubCode);
    }

    public void OnMonstersShouldDespawn(short mapId)
    {
    }

    public void OnAllSessionsShouldDisconnect(short mapId)
    {
        IngestForSlot(mapId, WarEndedToIdleSubCode);
    }

    private void IngestForSlot(short mapId, int subCode)
    {
        if (!TryResolveSlot(mapId, out var slot))
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        WriteInt32(payload, 0, slot);
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
