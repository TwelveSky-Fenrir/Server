using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone195NokSanBroadcaster(
    ZoneRegistry zones,
    ILogger<Zone195NokSanBroadcaster> logger) : IZone195NokSanBroadcaster
{
    private const int DataSize = 130;

    private const int ChallengerAppearedSort = 771;
    private const int CaptureCancelledSort = 772;
    private const int CountdownSort = 773;
    private const int CaptureSucceededSort = 774;
    private const int NokSanStateSort = 751;

    public void AnnounceChallengerAppeared(byte challengerTribe, string challengerName)
    {
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
        LogNameGap(CaptureSucceededSort, capturerName);
        Broadcast(CaptureSucceededSort, winningTribe, serverNumber);
    }

    public void AnnounceNokSanState(byte owningTribe, short serverNumber, Zone195NokSanStateSnapshot snapshot)
    {
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
