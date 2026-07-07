using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>
///     Sort: 1=view, 2=withdraw one slot (Value=slot index 0-49). Legacy's CZ_TRIBE_BANK_SEND
///     (<c>Server/ts25zone/S04_MyWork02.cpp:11560-11607</c>) recognizes only these two values; any other
///     Sort hits legacy's <c>default: Quit()</c> and disconnects. Sort 3 (deposit) is a Fenrir-only addition
///     with no legacy counterpart -- an unmodified legacy client never sends it -- gated identically to Sort
///     2 (Force Leader role + 3-sub-master quorum) in the tribe-bank service layer rather than left open to
///     any tribe member.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeBank, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeBankRequest : IIncomingPacket<TribeBankRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
