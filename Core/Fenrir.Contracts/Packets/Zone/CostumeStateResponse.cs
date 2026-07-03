using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_COSTUME_STATE_RECV (ZONE.h:1004-1016) — unicast response to CZ 90, builder
///     <c>B_COSTUME_STATE_RECV(tValue00..tValue07)</c> (S05_MyTransfer.cpp:1320-1333). BUILD-DEPENDENT
///     SIZE: the 8th int <see cref="CostumeDate" /> is under <c>USE_ENCHANT_COSTUME</c>, ACTIVE in EU33
///     → 33 bytes total (29 bytes in builds without that flag — do not omit this field for EU33).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CostumeState, ExpectedSize = 33)]
public readonly partial record struct CostumeStateResponse : IOutgoingPacket
{
    /// <summary><c>tValue00</c>: 0=ok, 1=invalid slot, 2=inventory full.</summary>
    public required int Result { get; init; }

    /// <summary><c>tValue01</c>: echo of CZ 90's <c>tSort</c>.</summary>
    public required int Sort { get; init; }

    /// <summary><c>tValue02</c>: echo of <c>tValue</c>/slot.</summary>
    public required int Value { get; init; }

    /// <summary><c>tValue03</c>: case 5 = destination inventory page; otherwise -1.</summary>
    public required int Page { get; init; }

    /// <summary><c>tValue04</c>: case 5 = X; otherwise -1.</summary>
    public required int PosX { get; init; }

    /// <summary><c>tValue05</c>: case 5 = Y; otherwise -1.</summary>
    public required int PosY { get; init; }

    /// <summary><c>tValue06</c>: case 5 = costume item index; otherwise -1.</summary>
    public required int ItemIndex { get; init; }

    /// <summary>
    ///     <c>tValue07</c> (<c>#ifdef USE_ENCHANT_COSTUME</c>, active): costume enchant date/value, case 5;
    ///     unset otherwise.
    /// </summary>
    public required int CostumeDate { get; init; }
}
