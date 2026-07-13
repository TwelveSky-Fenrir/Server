using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MultiItemCreate,
    ExpectedSize = 73)]
public readonly partial record struct MultiItemCreateResponse : IOutgoingPacket
{
    public required int Num { get; init; }

    public required int Page { get; init; }

    public required int Index1 { get; init; }

    public required int Index2 { get; init; }

    public required int Xy1 { get; init; }

    public required int Xy2 { get; init; }

    [FixedArray(8)] public required int[] ItemIndex { get; init; }

    [FixedArray(4)] public required int[] Value { get; init; }
}
