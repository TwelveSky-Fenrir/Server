using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(8)]
public readonly record struct TribeWorkTitlePayload : IFenrirWireType<TribeWorkTitlePayload>
{
    // 1-14, the new title CATEGORY (combined with the current rank to form the new aTitle).
    public required int TitleSort { get; init; }

    // Carried but unused by the legacy formula: the new rank derives from the CURRENT title's rank, not this value.
    public required int TitleLv { get; init; }
}
