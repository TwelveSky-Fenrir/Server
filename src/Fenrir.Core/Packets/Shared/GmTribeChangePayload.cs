using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmTribeChangePayload : IFenrirWireType<GmTribeChangePayload>
{
    public required int Tribe { get; init; }
}
