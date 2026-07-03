using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     DEFAULT_PDATA_RECV (STRUCT.h:1229-1238, 28 bytes) — declared INSIDE STRUCT.h's <c>pack(1)</c>
///     region (l.1016-1325); 7 ints, no padding possible regardless. Not a packet of its own: it is the
///     re-read layer <c>MyWork::ProcessForData</c> applies over the <c>tData</c> blob of
///     CZ_PROCESS_DATA_SEND (payload offset 4) for every "container move" <c>tSort</c> value
///     (208-232, 240-256, 3000, 250-253...). Exposed here as a plain deserializable wire type.
/// </summary>
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
