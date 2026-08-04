using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class TribeGuardCorridorStateDerivationSystem(
    TribeGuardCorridorCatalog catalog,
    TribeGuardCorridorState state,
    Lazy<ZoneCenterBroadcastIngestor> broadcastIngestor) : ISimulationSystem
{
    public const int GuardPostsPerSegment = 5;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var owned = catalog.GetSegmentsOwnedByZone(zone.MapId);
        if (owned.Count == 0)
            return;

        foreach (var (tribeId, segmentIndex) in owned)
            EvaluateSegment(zone, tribeId, segmentIndex);
    }

    private void EvaluateSegment(Zone zone, byte tribeId, byte segmentIndex)
    {
        if (!catalog.TryGetGuardPostSlots(tribeId, segmentIndex, out var slots))
            return;

        var anyAlive = false;
        foreach (var slot in slots)
            if (zone.TryGetMonster(slot, out var guard) && guard is not null && guard.Life > 0)
            {
                anyAlive = true;
                break;
            }

        var isOpen = !anyAlive;
        if (!state.TrySetOpen(tribeId, segmentIndex, isOpen))
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, tribeId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[sizeof(int)..], segmentIndex);
        broadcastIngestor.Value.Ingest(
            isOpen ? TribeGuardCorridorState.PassageOpenedEventCode : TribeGuardCorridorState.PassageBlockedEventCode,
            payload);
    }
}
