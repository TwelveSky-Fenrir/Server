using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneTransferCancel, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Leaving])]
public readonly partial record struct ZoneTransferCancelRequest : IIncomingPacket<ZoneTransferCancelRequest>
{
}
