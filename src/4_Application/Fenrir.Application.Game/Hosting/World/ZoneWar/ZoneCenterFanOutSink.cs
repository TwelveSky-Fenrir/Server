using System.Buffers.Binary;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Cluster.Link;
using Fenrir.Core.Opcodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
/// Applique côté shard le fan-out d'events monde reçu du CenterServer via le lien sortant (<see cref="ICenterLink"/>).
/// Route les trames op33 vers <see cref="ZoneCenterBroadcastIngestor.ApplyRelayedEvent"/> (effet d'état + relais
/// op94 aux clients de zone, sans écriture DB ni re-enqueue cross-shard). Inerte tant que
/// <see cref="GameServerOptions.WorldStateAuthority"/> n'est pas <see cref="WorldStateAuthorityMode.Center"/> —
/// au défaut <c>Shard</c> le fan-out est droppé (et de toute façon aucun shard ne pousse encore).
/// </summary>
public sealed class ZoneCenterFanOutSink(
    ZoneCenterBroadcastIngestor ingestor,
    IOptions<GameServerOptions> options,
    ILogger<ZoneCenterFanOutSink> logger) : ICenterFanOutSink
{
    // Trame S2S 135 o, opcode 1 o déjà retiré par S2SFrameReader => 134 o = int Sort (4 o LE) + byte[130] Data.
    private const int FrameBodySize = 134;

    private const int DataSize = 130;

    public void Receive(byte opcode, ReadOnlySpan<byte> payload)
    {
        if (options.Value.WorldStateAuthority != WorldStateAuthorityMode.Center)
        {
            // Mode Shard (défaut) : le shard est autoritaire, il n'applique pas le fan-out Center.
            logger.LogDebug("Center fan-out opcode {Opcode} dropped: shard is world-state authoritative", opcode);
            return;
        }

        if (payload.Length != FrameBodySize)
        {
            logger.LogWarning("Center fan-out opcode {Opcode}: unexpected body length {Length} (expected {Expected})",
                opcode, payload.Length, FrameBodySize);
            return;
        }

        switch (opcode)
        {
            case Opcodes.Center.Outgoing.WorldEvent:
                var sort = BinaryPrimitives.ReadInt32LittleEndian(payload[..4]);
                ingestor.ApplyRelayedEvent(sort, payload.Slice(4, DataSize));
                break;

            case Opcodes.Center.Outgoing.Party:
                // TODO(F4) : pas de consommateur party->client câblé côté shard aujourd'hui ; hook futur = un
                // broadcast par-zone miroir du relais op33. Droppé proprement en attendant.
                logger.LogDebug("Center fan-out party op57 received but no shard-side consumer wired -- dropping");
                break;

            default:
                logger.LogWarning("Center fan-out: unhandled opcode {Opcode}", opcode);
                break;
        }
    }
}
