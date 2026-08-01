using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TradeLock, ExpectedSize = 5)]
public readonly partial record struct TradeLockResponse : IOutgoingPacket
{
    public required int CheckMe { get; init; }
}
