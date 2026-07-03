using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.DemandZoneServerInfoSend,
    ExpectedSize = 13, AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ClDemandZoneServerInfoSend : IIncomingPacket<ClDemandZoneServerInfoSend>
{
    public required int AvatarPost { get; init; }
}
