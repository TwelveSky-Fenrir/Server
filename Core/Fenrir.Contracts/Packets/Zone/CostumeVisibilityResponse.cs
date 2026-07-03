using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_COSTUME_STATE2_RECV (ZONE.h:1359-1364) — unicast response to CZ 139, builder
///     <c>B_COSTUME_STATE2_RECV(MyUser*, tSort, tSort2, tSort3)</c> with USEND baked in
///     (S05_MyTransfer.cpp:1798-1805); single call site S04_MyWork02.cpp:15216.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CostumeVisibility,
    ExpectedSize = 13)]
public readonly partial record struct CostumeVisibilityResponse : IOutgoingPacket
{
    /// <summary>Echo of CZ 139's <c>tSort</c> (0/1).</summary>
    public required int Sort { get; init; }

    /// <summary>Always 0 in the single call site.</summary>
    public required int Sort2 { get; init; }

    /// <summary>Always 0 in the single call site.</summary>
    public required int Sort3 { get; init; }
}
