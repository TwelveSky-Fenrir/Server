using System.Buffers.Binary;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Center;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Center.Party;

public sealed class PartyEventCenterHandler(
    PartyRosterAuthority authority,
    ICenterPacketBroadcaster broadcaster,
    ILogger<PartyEventCenterHandler> logger)
    : IAsyncPacketHandler<PartyEventInbound>
{
    public ValueTask HandleAsync(PartyEventInbound packet, IPacketSession session, CancellationToken cancellationToken)
    {
        var data = packet.Data.AsSpan();
        var partyName = LegacyWireCodec.ReadFixedString(
            data.Slice(PartyEventProtocol.InboundPartyNameOffset, PartyEventProtocol.NameFieldLength));
        var avatarName = LegacyWireCodec.ReadFixedString(
            data.Slice(PartyEventProtocol.InboundAvatarNameOffset, PartyEventProtocol.NameFieldLength));

        var plan = authority.Apply(packet.Sort, partyName, avatarName);

        if (plan.EchoInbound)
            broadcaster.BroadcastToZones(new PartyEventOutbound { Sort = packet.Sort, Data = packet.Data });

        if (plan.Snapshot is { } snapshot)
            broadcaster.BroadcastToZones(new PartyEventOutbound
            {
                Sort = (int)PartyEventOperation.Info,
                Data = EncodeSnapshot(snapshot)
            });

        if (plan.Break is { } breakNotice)
            broadcaster.BroadcastToZones(new PartyEventOutbound
            {
                Sort = (int)PartyEventOperation.Break,
                Data = EncodeBreak(breakNotice)
            });

        logger.LogDebug(
            "op57 sub-code {Sort} for party '{Party}' / member '{Member}' from link {SessionId}: echo={Echo} snapshot={Snapshot} break={Break}",
            packet.Sort, partyName, avatarName, session.SessionId,
            plan.EchoInbound, plan.Snapshot is not null, plan.Break is not null);

        return ValueTask.CompletedTask;
    }

    private static byte[] EncodeSnapshot(PartyRosterSnapshot snapshot)
    {
        var data = new byte[PartyEventProtocol.DataSize];

        LegacyWireCodec.WriteFixedString(
            data.AsSpan(PartyEventProtocol.SnapshotPartyNameOffset, PartyEventProtocol.NameFieldLength),
            snapshot.PartyName);

        var slotCount = Math.Min(snapshot.Members.Length, PartyEventProtocol.MaxMembers);
        for (var i = 0; i < slotCount; i++)
            LegacyWireCodec.WriteFixedString(
                data.AsSpan(PartyEventProtocol.SnapshotFirstSlotOffset + i * PartyEventProtocol.NameFieldLength,
                    PartyEventProtocol.NameFieldLength),
                snapshot.Members[i]);

        BinaryPrimitives.WriteInt32LittleEndian(
            data.AsSpan(PartyEventProtocol.SnapshotDispositionOffset, sizeof(int)), (int)snapshot.Disposition);

        return data;
    }

    private static byte[] EncodeBreak(PartyBreakNotice breakNotice)
    {
        var data = new byte[PartyEventProtocol.DataSize];

        LegacyWireCodec.WriteFixedString(
            data.AsSpan(PartyEventProtocol.BreakPartyNameOffset, PartyEventProtocol.NameFieldLength),
            breakNotice.QueryName);
        LegacyWireCodec.WriteFixedString(
            data.AsSpan(PartyEventProtocol.BreakAvatarNameOffset, PartyEventProtocol.NameFieldLength),
            breakNotice.ResolvedName);

        return data;
    }
}
