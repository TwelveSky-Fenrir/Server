using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

// Not a packet of its own: re-read layer over CZ_PROCESS_DATA_SEND's tData blob (offset 4) for
// "container move" tSort values (208-232, 240-256, 3000, 250-253...).
[FenrirWireType(28)]
public readonly partial record struct DefaultPData : IFenrirWireType<DefaultPData>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Quantity1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }

    public required int XPost2 { get; init; }

    public required int YPost2 { get; init; }
}
