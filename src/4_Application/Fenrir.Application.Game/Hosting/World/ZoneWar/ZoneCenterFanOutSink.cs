using System.Buffers.Binary;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Cluster.Link;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

public sealed class ZoneCenterFanOutSink(
    ZoneCenterBroadcastIngestor ingestor,
    IOptions<GameServerOptions> options,
    ILogger<ZoneCenterFanOutSink> logger) : ICenterFanOutSink
{
    private const int FrameBodySize = 134;

    private const int DataSize = 130;

    public void Receive(byte opcode, ReadOnlySpan<byte> payload)
    {
        if (options.Value.WorldStateAuthority != WorldStateAuthorityMode.Center)
        {
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
                logger.LogDebug("Center fan-out party op57 received but no shard-side consumer wired -- dropping");
                break;

            default:
                logger.LogWarning("Center fan-out: unhandled opcode {Opcode}", opcode);
                break;
        }
    }
}
