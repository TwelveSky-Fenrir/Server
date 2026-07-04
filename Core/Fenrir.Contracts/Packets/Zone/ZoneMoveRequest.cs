using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>ZoneNumber must be 1..349; Sort 2 (GM) requires UserSort >= 1. On success the session enters "moving zone" — model as a session flag, not a <see cref="ZoneSessionState" />.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneMove,
    ExpectedSize = 21, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct ZoneMoveRequest : IIncomingPacket<ZoneMoveRequest>
{
    /// <summary>
    ///     Transfer reason: 2=GM, 3=death, 4=portal, 5=paying NPC, 6=NPC move, 7=return, 8=return item, 9=teleport
    ///     item, 10=NPC, 11=auto-&gt;zone 37, 12=zone 84.
    /// </summary>
    public required int Sort { get; init; }

    public required int ZoneNumber { get; init; }
    public required int PresentZoneNumber { get; init; }
}
