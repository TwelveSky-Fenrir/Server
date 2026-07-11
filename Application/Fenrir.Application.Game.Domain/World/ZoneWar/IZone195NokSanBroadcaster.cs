namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The five client-facing status notifications the Zone195 "Nok-San" solo-capture state machine
///     (<see cref="Fenrir.Application.Game.Domain.Simulation.Zone195NokSanSystem" />) emits, each a
///     cluster-broadcast op94 (<c>ZC_BROADCAST_INFO_RECV</c>) message through the center hub in the legacy
///     (Server/ts25zone/S07_MyGame01.cpp:8414,8442,8457,8485,8490,8580-8596). Modelled as a seam so the state
///     machine stays independent of the wire encoding: the concrete
///     <see cref="Zone195NokSanBroadcaster" /> performs the shard-wide fan-out, and a test can substitute a
///     capturing fake.
/// </summary>
/// <remarks>
///     WIRE-LAYOUT GAP: the translated behavior contract gives each notification's tSort code and its
///     semantic payload fields, but NOT the byte offsets of those fields within the op94 130-byte opaque
///     <c>Data</c> buffer. <see cref="Zone195NokSanBroadcaster" /> encodes the integer fields using this
///     codebase's own established op94 convention (int32 little-endian at offset i*4, exactly as
///     <see cref="ZoneEventBroadcaster" />'s own private <c>Broadcast(sort, ...ints)</c> does), and leaves the
///     character-NAME fields (<see cref="AnnounceChallengerAppeared" />/<see cref="AnnounceCaptureSucceeded" />)
///     unwritten with a flagged TODO, since a name string's exact offset/encoding is a wire-format detail no
///     citation in the contract pins down -- the same limitation <see cref="HolyStoneWarCycle" />'s own tSort
///     38 capture notice already documents ("carries only the winning tribe id, not the capturing character's
///     name the contract also calls for"). A wire-protocol owner should confirm the exact 751/771/774 payload
///     layout against Server/ts25zone/S07_MyGame01.cpp:8580-8596 before the name fields are wired.
/// </remarks>
public interface IZone195NokSanBroadcaster
{
    /// <summary>tSort 771: a challenger has appeared -- carries the challenger's tribe and character name.</summary>
    public void AnnounceChallengerAppeared(byte challengerTribe, string challengerName);

    /// <summary>tSort 772: the in-progress capture was cancelled -- carries the shard's server number.</summary>
    public void AnnounceCaptureCancelled(short serverNumber);

    /// <summary>tSort 773: countdown tick -- carries the remaining-time value and the server number.</summary>
    public void AnnounceCountdown(int remainingTime, short serverNumber);

    /// <summary>tSort 774: the capture succeeded -- carries the winning tribe, server number, and character name.</summary>
    public void AnnounceCaptureSucceeded(byte winningTribe, short serverNumber, string capturerName);

    /// <summary>
    ///     tSort 751: the full "Nok-San state" synchronization -- carries the new owning tribe, the server
    ///     number, the complete per-tribe stones-held counts, and the complete per-slot owner array.
    /// </summary>
    public void AnnounceNokSanState(byte owningTribe, short serverNumber, Zone195NokSanStateSnapshot snapshot);
}
