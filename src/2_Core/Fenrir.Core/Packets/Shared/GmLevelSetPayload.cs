using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmLevelSetPayload : IFenrirWireType<GmLevelSetPayload>
{
    public required int Level { get; init; }
}
