using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CostumeVisibility,
    ExpectedSize = 13)]
public readonly partial record struct CostumeVisibilityResponse : IOutgoingPacket
{
    public required int Sort { get; init; }

    public required int Sort2 { get; init; }

    public required int Sort3 { get; init; }
}
