using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeBank, ExpectedSize = 213)]
public readonly partial record struct TribeBankResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(50)] public required int[] TribeBankInfo { get; init; }
    public required int Money { get; init; }
}
