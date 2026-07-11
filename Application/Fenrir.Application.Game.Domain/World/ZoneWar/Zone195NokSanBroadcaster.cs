using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Cluster-broadcast implementation of <see cref="IZone195NokSanBroadcaster" />: encodes each Nok-San
///     notification as an op94 <see cref="ZoneEventInfoResponse" /> and fans it out to every player on every
///     zone this shard hosts -- the same shape (and the same "no inter-process hop left to model within one
///     shard, but only THIS shard's own maps are reached, not the whole cluster the legacy ts25center relayed
///     to") <see cref="ZoneEventBroadcaster" />'s own remarks document for the RvR sorts it covers.
/// </summary>
/// <remarks>
///     The integer payload fields are written with this codebase's established op94 convention -- int32
///     little-endian at offset i*4 in the opaque 130-byte <c>Data</c> buffer, exactly as
///     <see cref="ZoneEventBroadcaster" />'s own private <c>Broadcast</c> helper does. The character-name
///     fields (tSort 771/774) are deliberately NOT written: their exact byte offset/encoding within the op94
///     payload is a wire-format detail no citation in the contract pins down -- see
///     <see cref="IZone195NokSanBroadcaster" />'s own WIRE-LAYOUT GAP remark and
///     <see cref="HolyStoneWarCycle" />'s identical tSort-38 name gap. Same serialize-once / rent-once /
///     write-once / copy-N-times non-allocating fan-out (<see cref="ArrayPool{T}" />, per-recipient failure
///     logged-and-skipped) every other Zone broadcast helper in this codebase already uses.
/// </remarks>
public sealed class Zone195NokSanBroadcaster(
    ZoneRegistry zones,
    ILogger<Zone195NokSanBroadcaster> logger) : IZone195NokSanBroadcaster
{
    /// <summary>
    ///     Opaque op94 payload size (Server/Header/Protocol/DEFINE.h:69,377-381), same as
    ///     <see cref="ZoneEventBroadcaster" />.
    /// </summary>
    private const int DataSize = 130;

    private const int ChallengerAppearedSort = 771;
    private const int CaptureCancelledSort = 772;
    private const int CountdownSort = 773;
    private const int CaptureSucceededSort = 774;
    private const int NokSanStateSort = 751;

    public void AnnounceChallengerAppeared(byte challengerTribe, string challengerName)
    {
        // WIRE-LAYOUT GAP: challengerName not encoded (unknown offset) -- see class remarks. Only the tribe
        // field is written for now.
        LogNameGap(ChallengerAppearedSort, challengerName);
        Broadcast(ChallengerAppearedSort, challengerTribe);
    }

    public void AnnounceCaptureCancelled(short serverNumber)
    {
        Broadcast(CaptureCancelledSort, serverNumber);
    }

    public void AnnounceCountdown(int remainingTime, short serverNumber)
    {
        Broadcast(CountdownSort, remainingTime, serverNumber);
    }

    public void AnnounceCaptureSucceeded(byte winningTribe, short serverNumber, string capturerName)
    {
        // WIRE-LAYOUT GAP: capturerName not encoded (unknown offset) -- see class remarks.
        LogNameGap(CaptureSucceededSort, capturerName);
        Broadcast(CaptureSucceededSort, winningTribe, serverNumber);
    }

    public void AnnounceNokSanState(byte owningTribe, short serverNumber, Zone195NokSanStateSnapshot snapshot)
    {
        // Field order per the contract's own §Outputs description of the 751 payload (owning tribe, server
        // number, then the complete per-tribe counts, then the complete per-slot owners) --
        // Server/ts25zone/S07_MyGame01.cpp:8580-8596. Exact interleaving/offsets are the wire-layout gap this
        // class's remarks flag; a wire owner should confirm against that source.
        var fields = new int[2 + snapshot.StonesHeld.Length + snapshot.Owners.Length];
        fields[0] = owningTribe;
        fields[1] = serverNumber;

        var cursor = 2;
        for (var i = 0; i < snapshot.StonesHeld.Length; i++)
            fields[cursor++] = snapshot.StonesHeld[i];
        for (var i = 0; i < snapshot.Owners.Length; i++)
            fields[cursor++] = snapshot.Owners[i];

        Broadcast(NokSanStateSort, fields);
    }

    private void LogNameGap(int sort, string name)
    {
        logger.LogDebug(
            "Zone195 Nok-San tSort {Sort}: character name '{Name}' not encoded into the op94 payload (unknown wire offset -- documented gap)",
            sort, name);
    }

    private void Broadcast(int sort, params ReadOnlySpan<int> fields)
    {
        var data = new byte[DataSize];
        for (var i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), fields[i]);

        var response = new ZoneEventInfoResponse { Sort = sort, Data = data };
        BroadcastToEveryZone(in response);
    }

    /// <summary>
    ///     Serialize-once / rent-once / write-once / copy-N-times shard-wide fan-out with a per-recipient
    ///     failure isolated (logged, skipped) -- verbatim the idiom
    ///     <c>ZoneEventBroadcaster.BroadcastToEveryZone</c> documents for why a single already-completed
    ///     transport pipe writer (an ordinary disconnect race) must not cut off delivery to every remaining
    ///     recipient in the same call.
    /// </summary>
    private void BroadcastToEveryZone<TPacket>(in TPacket response) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            foreach (var player in zone.Players)
                try
                {
                    if (player.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Zone195 Nok-San broadcast to character {RecipientId} (zone {MapId}) failed",
                        player.CharacterId, zone.MapId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
