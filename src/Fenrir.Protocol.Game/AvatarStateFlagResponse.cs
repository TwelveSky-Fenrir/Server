using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarStateFlag, ExpectedSize = 25)]
public readonly partial record struct AvatarStateFlagResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int Sort { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
    public required int Value03 { get; init; }
}
