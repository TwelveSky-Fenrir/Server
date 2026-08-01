using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRewardClaim, ExpectedSize = 57)]
public readonly partial record struct HeroRewardClaimResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Page { get; init; }
    public required int Index1 { get; init; }
    public required int Index2 { get; init; }
    public required int Xy1 { get; init; }
    public required int Xy2 { get; init; }
    [FixedArray(8)] public required int[] ItemIndex { get; init; }
}
