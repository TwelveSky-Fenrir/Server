using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CostumeState, ExpectedSize = 33)]
public readonly partial record struct CostumeStateResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Sort { get; init; }

    public required int Value { get; init; }

    public required int Page { get; init; }

    public required int PosX { get; init; }

    public required int PosY { get; init; }

    public required int ItemIndex { get; init; }

    public required int CostumeDate { get; init; }
}
