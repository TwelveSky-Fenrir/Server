using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct TribeWorkTitlePayload : IFenrirWireType<TribeWorkTitlePayload>
{
    public required int TitleSort { get; init; }

    public required int TitleLv { get; init; }
}
