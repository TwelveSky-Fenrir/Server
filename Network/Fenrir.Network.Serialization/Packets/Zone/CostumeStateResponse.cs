using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// USE_ENCHANT_COSTUME is active: CostumeDate is present (33-byte payload, not 29).
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
