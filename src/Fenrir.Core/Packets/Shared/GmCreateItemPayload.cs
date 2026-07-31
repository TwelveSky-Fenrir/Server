using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmCreateItemPayload : IFenrirWireType<GmCreateItemPayload>
{
    public required int ItemId { get; init; }
}
