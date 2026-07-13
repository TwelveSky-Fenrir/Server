using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeLock, ExpectedSize = 5)]
public readonly partial record struct TradeLockResponse : IOutgoingPacket
{
    public required int CheckMe { get; init; }
}
