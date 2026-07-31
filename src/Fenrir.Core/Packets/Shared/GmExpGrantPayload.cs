using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmExpGrantPayload : IFenrirWireType<GmExpGrantPayload>
{
    public required int Type { get; init; }

    public required int Exp { get; init; }
}
