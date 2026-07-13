using System.Buffers.Binary;
using Fenrir.Cluster.Wire.Packets;
using Fenrir.Core.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Party;

/// <summary>
///     Thin op57 wire boundary: decodes the two inbound names out of the fixed tData region, delegates the
///     authoritative roster decision to <see cref="PartyRosterAuthority" />, then encodes and fans out the
///     resulting broadcast(s) to every connected zone link via <see cref="ICenterLinkBroadcaster" />. No party
///     rule lives here — only marshalling.
///     <para>
///         The "sender must be a zone link" precondition is enforced upstream by the packet's
///         <c>AllowedStates = [Authenticated]</c> gate (an unauthenticated link is closed before dispatch). The
///         Center does not yet distinguish a zone link from a login link in <c>CenterSessionState</c>; op57 is
///         only ever sent by zones, so this is adequate for now — a finer link-type gate is a small follow-up
///         outside this unit.
///     </para>
/// </summary>
public sealed class PartyEventCenterHandler(
    PartyRosterAuthority authority,
    ICenterLinkBroadcaster broadcaster,
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

        // Emission order matters: raw echo first, then the authoritative snapshot (JOIN fans out both).
        if (plan.EchoInbound)
            broadcaster.BroadcastToZones(new PartyEventOutbound { Sort = packet.Sort, Data = packet.Data });

        if (plan.Snapshot is { } snapshot)
            broadcaster.BroadcastToZones(new PartyEventOutbound
            {
                Sort = (int)PartyEventOperation.Info,
                Data = EncodeSnapshot(snapshot),
            });

        if (plan.Break is { } breakNotice)
            broadcaster.BroadcastToZones(new PartyEventOutbound
            {
                Sort = (int)PartyEventOperation.Break,
                Data = EncodeBreak(breakNotice),
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
        // Unused slots stay zero (empty names). Trailing 48 bytes stay zero.

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
        // Trailing 4-byte sort (offset 26) left zero: the contract does not specify a value for it.

        return data;
    }
}
