using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmClearInventoryPayload : IFenrirWireType<GmClearInventoryPayload>
{
    public required int PageSelector { get; init; }
}
