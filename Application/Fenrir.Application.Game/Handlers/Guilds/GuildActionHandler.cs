using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Guilds;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Guilds;

/// <summary>
///     CZ_GUILD_WORK_SEND (opcode 75) -- the generic guild sub-command channel. Dead sub-commands reproduce
///     their exact legacy shape: 11 always aborts; 12/13 are a silent no-op (the legacy's own abort call is
///     commented out); anything else falls to the default abort.
/// </summary>
/// <remarks>
///     Membership changes (join/leave/kick/promote/transfer) still only mirror onto the specific character(s)
///     whose own <c>PlayerRuntimeState</c> changed, via <see cref="GuildMembershipZoneCommand" /> -- a guild-wide
///     GUILD_INFO push for those would still leave every other member's roster view stale on its own next query,
///     but membership rows are looked up fresh every time regardless. Notice/AGM/title/buff, by contrast, mutate
///     something every member's already-cached GUILD_INFO should reflect immediately, so those four additionally
///     broadcast the refreshed GUILD_INFO to every currently connected member via
///     <see cref="GuildInfoBroadcaster" />, not just the actor (who already gets it through <see cref="SendResult" />).
/// </remarks>
public sealed class GuildActionHandler(
    ZoneRegistry zones,
    IGuildRepository guilds,
    ICharacterRepository characters,
    GuildInviteRegistry invites,
    ILogger<GuildActionHandler> logger) : IAsyncPacketHandler<GuildActionRequest>
{
    private const int CreateGuildMoneyCost = 10_000_000;
    private const int MaxSubMasters = 2;

    public async ValueTask HandleAsync(GuildActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        // Every tSort here shares the same per-character economy-adjacent state (guild membership, money).
        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        switch (packet.Sort)
        {
            case 1:
                await HandleCreateAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 2:
                await HandleInfoAsync(session, zoneSession, state, ct);
                return;
            case 3:
                await HandleInviteFinalizeAsync(packet, session, zoneSession, state, characterId, ct);
                return;
            case 4:
                await HandleExitAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 5:
                await HandleNoticeAsync(packet, session, zoneSession, state, ct);
                return;
            case 6:
                await HandleDisbandAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 7:
                await HandleUpgradeAsync(packet, session, zoneSession, state, characterId, ct);
                return;
            case 8:
                await HandleKickAsync(packet, session, zoneSession, state, ct);
                return;
            case 9:
                await HandleAgmAsync(packet, session, zoneSession, state, ct);
                return;
            case 10:
                await HandleTitleAsync(packet, session, zoneSession, state, ct);
                return;
            case 11:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case 12:
            case 13:
                return;
            case 14:
                await HandleBuffAsync(packet, session, zoneSession, state, ct);
                return;
            case 17:
                await HandleTransferAsync(packet, session, zoneSession, zone, state, characterId, ct);
                return;
            case 1001:
                await HandleLogoAsync(packet, session, zoneSession, state, ct);
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    /// <summary>tSort 1 -- create a guild. Requires level &gt;=30, sufficient money, a non-empty name, and no existing guild.</summary>
    private async ValueTask HandleCreateAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not null || !GuildWorkCreatePayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var name = payload.GuildName.Trim();
        if (name.Length == 0 || state.Level < 30)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        int guildId;
        try
        {
            // Guild created before money is debited; a failed debit below rolls it back rather than
            // leaving an unpaid guild (Fenrir has no cached Money field to pre-check against).
            guildId = await guilds.CreateAsync(name, characterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} guild create failed for name {GuildName}",
                characterId, name);
            SendResult(session, 1, GuildInfoProjection.Empty(), 1);
            return;
        }

        try
        {
            await characters.AdjustMoneyAsync(characterId, -CreateGuildMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} guild create money debit failed -- rolling back guild {GuildId}",
                characterId, guildId);
            await guilds.DisbandAsync(guildId, ct);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var info = await BuildGuildInfoAsync(guildId, ct);
        SendResult(session, 1, info);

        await zone.PostGuildCommandAndWaitAsync(
            new GuildMembershipZoneCommand(characterId, guildId, name, GuildRoleCodec.WireRoleToDb(0), ""), ct);
    }

    /// <summary>tSort 2 -- info/roster refresh; must already be in a guild.</summary>
    private async ValueTask HandleInfoAsync(IPacketSession session, ZoneClientSession zoneSession,
        PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, 2, await BuildGuildInfoAsync(guildId, ct));
    }

    /// <summary>
    ///     tSort 3 -- invite finalize. Requires master/sub-master role and a pending accepted invite. Member cap =
    ///     Grade*10.
    /// </summary>
    private async ValueTask HandleInviteFinalizeAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMasterOrSubMaster(state.GuildRoleDb))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!invites.TryConsumeAccepted(characterId, out var inviteeId))
        {
            SendResult(session, 3, GuildInfoProjection.Empty(), 1);
            return;
        }

        var guild = await guilds.GetByIdAsync(guildId, ct);
        if (guild is null)
        {
            SendResult(session, 3, GuildInfoProjection.Empty(), 1);
            return;
        }

        var roster = await guilds.GetRosterAsync(guildId, ct);
        if (roster.Count >= guild.Grade * 10)
        {
            SendResult(session, 3, GuildInfoProjection.Empty(), 2);
            return;
        }

        try
        {
            await guilds.AddMemberAsync(guildId, inviteeId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} guild invite-finalize add failed for invitee {InviteeId}",
                characterId, inviteeId);
            SendResult(session, 3, GuildInfoProjection.Empty(), 1);
            return;
        }

        SendResult(session, 3, await BuildGuildInfoAsync(guildId, ct));

        if (zones.TryGetPlayerAndZone(inviteeId, out _, out var inviteeZone))
            inviteeZone.PostGuildCommand(new GuildMembershipZoneCommand(inviteeId, guildId, guild.Name,
                GuildRoleCodec.WireRoleToDb(2), ""));
    }

    /// <summary>tSort 4 -- voluntary exit. The master cannot leave (must transfer/disband instead).</summary>
    private async ValueTask HandleExitAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || GuildRoleCodec.IsMaster(state.GuildRoleDb))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await guilds.RemoveMemberAsync(guildId, characterId, ct);

        SendResult(session, 4, await BuildGuildInfoAsync(guildId, ct));

        await zone.PostGuildCommandAndWaitAsync(new GuildMembershipZoneCommand(characterId, null, "", 0, ""), ct);
    }

    /// <summary>tSort 5 -- guild notice, master only.</summary>
    private async ValueTask HandleNoticeAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkNoticePayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        for (byte i = 0; i < payload.Notices.Length; i++)
            await guilds.SetNoticeAsync(guildId, i, payload.Notices[i].Trim(), ct);

        var info = await BuildGuildInfoAsync(guildId, ct);
        SendResult(session, 5, info);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 5, info, state.CharacterId);
    }

    /// <summary>tSort 6 -- disband, master only, requires exactly 1 remaining member.</summary>
    private async ValueTask HandleDisbandAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var roster = await guilds.GetRosterAsync(guildId, ct);
        if (roster.Count != 1)
        {
            SendResult(session, 6, GuildInfoProjection.Empty(), 2);
            return;
        }

        await guilds.DisbandAsync(guildId, ct);

        SendResult(session, 6, GuildInfoProjection.Empty());

        await zone.PostGuildCommandAndWaitAsync(new GuildMembershipZoneCommand(characterId, null, "", 0, ""), ct);
    }

    /// <summary>tSort 7 -- grade upgrade, master only; member count must already be at cap, level/cost thresholds per grade.</summary>
    private async ValueTask HandleUpgradeAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var guild = await guilds.GetByIdAsync(guildId, ct);
        if (guild is null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var roster = await guilds.GetRosterAsync(guildId, ct);
        if (roster.Count < guild.Grade * 10)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var (requiredLevel, cost) = guild.Grade switch
        {
            1 => (50, 20_000_000),
            2 => (70, 30_000_000),
            3 => (90, 40_000_000),
            4 => (113, 50_000_000),
            _ => (-1, -1)
        };

        if (requiredLevel < 0 || state.Level < requiredLevel)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var previousGrade = guild.Grade;
        try
        {
            // Grade incremented before money is debited; a failed debit below rolls it back.
            await guilds.SetGradeAsync(guildId, previousGrade + 1, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} guild upgrade failed for guild {GuildId}",
                characterId, guildId);
            SendResult(session, 7, GuildInfoProjection.Empty(), 1);
            return;
        }

        try
        {
            await characters.AdjustMoneyAsync(characterId, -cost, 0, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} guild upgrade money debit failed -- rolling back guild {GuildId} to grade {PreviousGrade}",
                characterId, guildId, previousGrade);
            await guilds.SetGradeAsync(guildId, previousGrade, ct);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, 7, await BuildGuildInfoAsync(guildId, ct));
    }

    /// <summary>
    ///     tSort 8 -- kick, master only. Target resolved by name, independent of online state; the master cannot be
    ///     kicked.
    /// </summary>
    private async ValueTask HandleKickAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkKickPayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var roster = await guilds.GetRosterAsync(guildId, ct);
        var target = FindMember(roster, targetName);
        if (target is null || GuildRoleCodec.IsMaster(target.Role))
        {
            SendResult(session, 8, GuildInfoProjection.Empty(), 1);
            return;
        }

        await guilds.RemoveMemberAsync(guildId, target.CharacterId, ct);

        SendResult(session, 8, await BuildGuildInfoAsync(guildId, ct));

        if (zones.TryGetPlayerAndZone(target.CharacterId, out _, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, null, "", 0, ""));
    }

    /// <summary>
    ///     tSort 9 -- AGM promote (1)/demote (2), master only. Promote refused if already at
    ///     <see cref="MaxSubMasters" /> or the target isn't a plain member; demote requires the target
    ///     currently be a sub-master.
    /// </summary>
    private async ValueTask HandleAgmAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkAgmPayload.TryRead(packet.Data, out var payload) || payload.GuildRole is < 1 or > 2)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var roster = await guilds.GetRosterAsync(guildId, ct);
        var target = FindMember(roster, targetName);
        if (target is null)
        {
            SendResult(session, 9, GuildInfoProjection.Empty(), 1);
            return;
        }

        const byte dbMember = 0;
        const byte dbSubMaster = 1;

        if (payload.GuildRole == 1)
        {
            var subMasterCount = roster.Count(r => r.Role == dbSubMaster);
            if (subMasterCount >= MaxSubMasters || target.Role != dbMember)
            {
                SendResult(session, 9, GuildInfoProjection.Empty(), 1);
                return;
            }
        }
        else
        {
            if (target.Role != dbSubMaster)
            {
                SendResult(session, 9, GuildInfoProjection.Empty(), 1);
                return;
            }
        }

        var newRole = payload.GuildRole == 1 ? dbSubMaster : dbMember;
        await guilds.SetRoleAsync(guildId, target.CharacterId, newRole, ct);
        await guilds.SetCallNameAsync(guildId, target.CharacterId, "", ct);

        var info = await BuildGuildInfoAsync(guildId, ct);
        SendResult(session, 9, info);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 9, info, state.CharacterId);

        if (zones.TryGetPlayerAndZone(target.CharacterId, out var targetState, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, guildId,
                targetState.GuildName, newRole, ""));
    }

    /// <summary>tSort 10 -- member title/CallName, master only. Target must be a member, not the master.</summary>
    private async ValueTask HandleTitleAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkTitlePayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var roster = await guilds.GetRosterAsync(guildId, ct);
        var target = FindMember(roster, targetName);
        if (target is null || GuildRoleCodec.IsMaster(target.Role))
        {
            SendResult(session, 10, GuildInfoProjection.Empty(), 1);
            return;
        }

        var callName = payload.CallName.Trim();
        await guilds.SetCallNameAsync(guildId, target.CharacterId, callName, ct);

        var info = await BuildGuildInfoAsync(guildId, ct);
        SendResult(session, 10, info);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 10, info, state.CharacterId);

        if (zones.TryGetPlayerAndZone(target.CharacterId, out var targetState, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, guildId,
                targetState.GuildName, targetState.GuildRoleDb, callName));
    }

    /// <summary>
    ///     tSort 14 -- buff type choice, master/sub-master only. A plain member gets a clean tResult=4, not
    ///     an abort. Requires at least 1 minute of buff-time reserve (only ever recharged by tSort 15's
    ///     guild scrolls). The buff's actual gameplay stat effect is undocumented, so only the state machine
    ///     (choose a type, track remaining reserve) is implemented -- no stat bonus is invented. Activation
    ///     stamps BuffTimeForDiff to now: <see cref="GuildBuffDecayHost" /> reads that checkpoint to burn
    ///     the reserve down over real time, so it must restart from here, never from whatever stale value (or
    ///     0) the row already carried.
    /// </summary>
    private async ValueTask HandleBuffAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!GuildRoleCodec.IsMasterOrSubMaster(state.GuildRoleDb))
        {
            SendResult(session, 14, GuildInfoProjection.Empty(), 4);
            return;
        }

        if (!GuildWorkBuffPayload.TryRead(packet.Data, out var payload) || payload.GuildBuffType is < 0 or > 4)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var guild = await guilds.GetByIdAsync(guildId, ct);
        if (guild is null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (guild.BuffTime < 1)
        {
            SendResult(session, 14, GuildInfoProjection.Empty(), 2);
            return;
        }

        try
        {
            await guilds.SetBuffAsync(guildId, payload.GuildBuffType, 1, guild.BuffTime, DateTime.UtcNow.Ticks, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Guild {GuildId} buff type update failed", guildId);
            SendResult(session, 14, GuildInfoProjection.Empty(), 5);
            return;
        }

        var info = await BuildGuildInfoAsync(guildId, ct);
        SendResult(session, 14, info);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 14, info, state.CharacterId);
    }

    /// <summary>
    ///     tSort 17 -- transfer leadership, master only. Target must be in the actor's own zone. Success
    ///     replies tResult=2, not 0 -- a verified legacy quirk the client expects.
    /// </summary>
    private async ValueTask HandleTransferAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkTransferPayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newMasterName = payload.NewMasterName.Trim();
        if (newMasterName.Length == 0)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        PlayerRuntimeState? newMaster = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, newMasterName, StringComparison.OrdinalIgnoreCase))
            {
                newMaster = candidate;
                break;
            }

        if (newMaster is null)
        {
            SendResult(session, 17, GuildInfoProjection.Empty(), 1);
            return;
        }

        try
        {
            await guilds.SetMasterAsync(guildId, newMaster.CharacterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} guild leadership transfer to {NewMasterId} failed",
                characterId, newMaster.CharacterId);
            SendResult(session, 17, GuildInfoProjection.Empty(), 1);
            return;
        }

        SendResult(session, 17, await BuildGuildInfoAsync(guildId, ct), 2);

        await zone.PostGuildCommandAndWaitAsync(
            new GuildMembershipZoneCommand(characterId, guildId, state.GuildName, GuildRoleCodec.WireRoleToDb(2), ""),
            ct);
        zone.PostGuildCommand(new GuildMembershipZoneCommand(newMaster.CharacterId, guildId, state.GuildName,
            GuildRoleCodec.WireRoleToDb(0), ""));
    }

    /// <summary>
    ///     tSort 1001 -- logo, master only. Always replies with an empty GuildInfo, even on success (matches
    ///     a legacy quirk). Deliberately stricter than legacy here: the legacy's own role check accidentally
    ///     also passes guildless characters (a zero-effect legacy accident); Fenrir correctly rejects them.
    /// </summary>
    private async ValueTask HandleLogoAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, PlayerRuntimeState state, CancellationToken ct)
    {
        if (!GuildRoleCodec.IsMaster(state.GuildRoleDb) || state.GuildId is not { } guildId ||
            !GuildWorkLogoPayload.TryRead(packet.Data, out var payload))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        try
        {
            await guilds.SetLogoAsync(guildId, payload.Value, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Guild {GuildId} logo update failed", guildId);
        }

        SendResult(session, 1001, GuildInfoProjection.Empty());
    }

    private async Task<GuildInfo> BuildGuildInfoAsync(int guildId, CancellationToken ct)
    {
        var guildTask = guilds.GetByIdAsync(guildId, ct);
        var rosterTask = guilds.GetRosterAsync(guildId, ct);
        var noticesTask = guilds.GetNoticesAsync(guildId, ct);
        await Task.WhenAll(guildTask.AsTask(), rosterTask.AsTask(), noticesTask.AsTask());

        return guildTask.Result is { } guild
            ? GuildInfoProjection.Build(guild, rosterTask.Result, noticesTask.Result)
            : GuildInfoProjection.Empty();
    }

    private static GuildRosterRowDto? FindMember(IReadOnlyList<GuildRosterRowDto> roster, string name)
    {
        foreach (var row in roster)
            if (string.Equals(row.CharacterName, name, StringComparison.OrdinalIgnoreCase))
                return row;
        return null;
    }

    private static void SendResult(IPacketSession session, int sort, GuildInfo info, int result = 0)
    {
        session.Send(new GuildActionResponse { Result = result, Sort = sort, GuildInfo = info });
    }
}
