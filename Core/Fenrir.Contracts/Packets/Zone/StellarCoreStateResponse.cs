using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_STELLAR_STATE_RECV (ZONE.h:1018-1027) — unicast response to CZ 153, builder
///     <c>B_STELLAR_STATE_RECV(tValue00..tValue06)</c> (S05_MyTransfer.cpp:1854-1864). Structural twin of
///     ZC 108 WITHOUT the 8th int (no <c>#ifdef USE_ENCHANT_COSTUME</c> here): 29 bytes total — do not
///     copy the 33-byte ZC 108 layout here.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.StellarCoreState, ExpectedSize = 29)]
public readonly partial record struct StellarCoreStateResponse : IOutgoingPacket
{
    /// <summary><c>tValue00</c>: 0=ok, 1=invalid slot, 2=inventory full.</summary>
    public required int Result { get; init; }

    /// <summary><c>tValue01</c>: echo of CZ 153's <c>tSort</c>.</summary>
    public required int Sort { get; init; }

    /// <summary><c>tValue02</c>: echo of <c>tValue</c>/slot.</summary>
    public required int Value { get; init; }

    /// <summary><c>tValue03</c>: case 5; otherwise -1.</summary>
    public required int Page { get; init; }

    /// <summary><c>tValue04</c>: case 5; otherwise -1.</summary>
    public required int PosX { get; init; }

    /// <summary><c>tValue05</c>: case 5; otherwise -1.</summary>
    public required int PosY { get; init; }

    /// <summary><c>tValue06</c>: case 5; otherwise -1.</summary>
    public required int ItemIndex { get; init; }
}
