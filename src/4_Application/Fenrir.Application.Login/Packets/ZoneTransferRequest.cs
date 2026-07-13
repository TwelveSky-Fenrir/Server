using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ZoneTransfer,
    ExpectedSize = 13, AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ZoneTransferRequest : IIncomingPacket<ZoneTransferRequest>
{
    public required int AvatarPost { get; init; }
}
