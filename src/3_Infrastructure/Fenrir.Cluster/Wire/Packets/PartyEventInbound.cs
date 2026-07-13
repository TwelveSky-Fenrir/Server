using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Cluster.Wire;

namespace Fenrir.Cluster.Wire.Packets;

/// <summary>
///     Zone → Center authoritative party (group) sync, legacy op57 (<c>ZR_ZONE_BROADCAST_FOR_RELAY_SEND</c>,
///     ZONE.h:53-65). One inbound envelope carries all five party sub-codes (104 JOIN / 106 LEAVE /
///     107 KICK / 108 INFO / 109 BREAK) in <see cref="Sort" />; the acted-on names live inside the fixed
///     130-byte <see cref="Data" /> region (offset 0 = partyName[13], offset 13 = avatarName[13], rest
///     zero — <c>#pragma pack(1)</c>, no padding). Modelled as raw 130 bytes (not name-typed fields) to stay
///     byte-identical to the <c>WorldEventInbound</c> op33 sibling and to honour the contract's aliasing note
///     (the legacy payload structs re-read the envelope <c>tSort</c> at buffer offset 1, so tData begins with
///     the two names, never with an int). Decoded by <c>PartyRosterAuthority</c>.
///     <para>
///         Opcode passed as the literal <c>57</c>: <c>Opcodes.Center</c> (in Fenrir.Core) is outside this
///         Cluster unit's edit scope; adding <c>Opcodes.Center.Incoming.Party = 57</c> and swapping it in is a
///         reconciliation task for Main.
///     </para>
/// </summary>
[FenrirPacket(FenrirServer.Center, FenrirDirection.Incoming, 57, ExpectedSize = 135,
    AllowedStates = [(byte)CenterSessionState.Authenticated])]
public readonly partial record struct PartyEventInbound : IIncomingPacket<PartyEventInbound>
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
