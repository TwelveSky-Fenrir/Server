using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.CreateAvatarSend2,
    ExpectedSize = 50, AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ClCreateAvatarSend2 : IIncomingPacket<ClCreateAvatarSend2>
{
    public required int AvatarPost { get; init; }
    public required int Tribe { get; init; }
    public required int PreviousTribe { get; init; }
    public required int Gender { get; init; }
    public required int Head { get; init; }
    public required int Face { get; init; }
    public required int Weapon { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
