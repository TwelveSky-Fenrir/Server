using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct TeleportTollData : IFenrirWireType<TeleportTollData>
{
    public required int Money { get; init; }
}
