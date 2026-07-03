using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     BLOOD_SHOP (STRUCT.h:1429-1433, 604 bytes) — the full blood-mark shop catalog, 50 nested
///     <see cref="BloodItem" /> slots. Declared outside pack(1) but every member is a 4-byte-aligned
///     int/int-block → no padding. <see cref="BloodNum" /> is the count of valid entries (&lt;= 50);
///     slots beyond it are still sent on the wire (zeros).
/// </summary>
[FenrirWireType(604)]
public readonly partial record struct BloodShop : IFenrirWireType<BloodShop>
{
    public required int BloodNum { get; init; }

    [FixedArray(50)] public required BloodItem[] Data { get; init; }
}
