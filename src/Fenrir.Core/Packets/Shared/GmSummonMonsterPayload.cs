using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmSummonMonsterPayload : IFenrirWireType<GmSummonMonsterPayload>
{
    public required int Value { get; init; }
}
