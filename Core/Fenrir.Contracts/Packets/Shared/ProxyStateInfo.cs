using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     PROXY_STATE_INFO (STRUCT.h:1734-1740) — C++ <c>sizeof</c> is 52 bytes, but the generator's
///     <c>[Reserved(N)]</c> can only mark padding BEFORE a field, never a trailing padding zone
///     (verified: <c>Analysis/Fenrir.Generators.Analysis/Scanning/FieldScanner.cs</c>). The 3 real
///     fields below account for exactly 50 bytes; the remaining 2 bytes of QUEUE padding (natural
///     4-byte alignment after the trailing <c>char[25]</c>) are NOT part of this wire type — they are
///     carried by the parent packet via <c>[Reserved(2)]</c> on the field that follows the embedded
///     <see cref="ProxyStateInfo" /> (its only client-facing use: ZC_GET_DEPUTY_PSHOP_RECV/ZC 134,
///     field <c>CheckChangeActionState</c>). The combined total on the wire still matches the C++
///     <c>sizeof</c> of 52 bytes exactly.
/// </summary>
[FenrirWireType(50)]
public readonly partial record struct ProxyStateInfo : IFenrirWireType<ProxyStateInfo>
{
    [FixedArray(3)] public required float[] Location { get; init; }

    [FixedString(13)] public required string Name { get; init; }

    [FixedString(25)] public required string PshopName { get; init; }
}
