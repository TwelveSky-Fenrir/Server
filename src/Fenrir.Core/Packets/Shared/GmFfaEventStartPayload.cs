using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmFfaEventStartPayload : IFenrirWireType<GmFfaEventStartPayload>
{
    public required int Time { get; init; }
}
