using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.DeleteAvatarSend, ExpectedSize = 21,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ClDeleteAvatarSend : IIncomingPacket<ClDeleteAvatarSend>
{
    public required int AvatarPost { get; init; }
    public required int Unknow1 { get; init; }
    public required int Unknow2 { get; init; }
}
