using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Cluster.Wire.Packets;

/// <summary>
///     Center → all-zones fan-out for authoritative party sync, legacy op57
///     (<c>RZ_ZONE_BROADCAST_FOR_RELAY_RECV</c>, RELAY.h:16-28). Shares the identical 135-byte envelope with
///     <see cref="PartyEventInbound" />. Three distinct <see cref="Data" /> shapes travel through this one
///     packet, discriminated by <see cref="Sort" />:
///     <list type="bullet">
///         <item>raw echo — <c>Sort</c> = the original sub-code, <c>Data</c> = the inbound bytes verbatim;</item>
///         <item>snapshot — <c>Sort</c> = 108, <c>Data</c> = <c>PARTY_INFO_RECV</c> (partyName + 5 slot names +
///         pSort disposition, STRUCT.h:1064-1073);</item>
///         <item>break — <c>Sort</c> = 109, <c>Data</c> = <c>PARTY_BREAK_RECV</c> (partyName + avatarName01 +
///         trailing sort, STRUCT.h:1049-1054).</item>
///     </list>
///     <see cref="Data" /> is always emitted at full 130 bytes regardless of used content. Built by the
///     party handler; fanned out to every connected zone link via <c>ICenterLinkBroadcaster</c>.
/// </summary>
[FenrirPacket(FenrirServer.Center, FenrirDirection.Outgoing, 57, ExpectedSize = 135)]
public readonly partial record struct PartyEventOutbound : IOutgoingPacket
{
    public required int Sort { get; init; }

    [FixedArray(130)] public required byte[] Data { get; init; }
}
