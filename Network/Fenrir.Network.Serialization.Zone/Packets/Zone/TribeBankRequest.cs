using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     Sort: 1=view, 2=deposit into a specific slot (Value=slot index 0-49; the caller's entire current money
///     is debited and credited to the tribe-bank slot, Force Leader role + 3-sub-master quorum required).
///     Legacy's CZ_TRIBE_BANK_SEND (<c>Server/ts25zone/S04_MyWork02.cpp:11560-11607</c>) recognizes only these
///     two values; any other Sort hits legacy's <c>default: Quit()</c> and disconnects. There is no legacy
///     sub-command on this opcode that withdraws bank funds to a player.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeBank, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeBankRequest : IIncomingPacket<TribeBankRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
