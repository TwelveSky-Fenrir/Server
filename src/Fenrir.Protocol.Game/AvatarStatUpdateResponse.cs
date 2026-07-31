using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarStatUpdate, ExpectedSize = 13)]
public readonly partial record struct AvatarStatUpdateResponse : IOutgoingPacket
{
    public required int Sort { get; init; }

    public required int Value { get; init; }

    public required int Value2 { get; init; }
}
