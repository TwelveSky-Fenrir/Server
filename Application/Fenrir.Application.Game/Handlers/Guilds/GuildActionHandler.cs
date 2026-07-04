using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Guilds;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Guilds;

/// <summary>
///     CZ_GUILD_WORK_SEND (opcode 75) -- the generic guild sub-command channel (doc 10 §1, verified in
///     full against <c>Server/ts25zone/S04_MyWork02.cpp:9969-10703</c>). Every tSort that is still alive in
///     EU33 is dispatched here: 1 create, 2 info/roster, 3 invite finalize, 4 exit, 5 notice, 6 disband,
///     7 upgrade grade, 8 kick, 9 AGM promote/demote, 10 member title, 14 buff type (USE_GUILD_BUFF), 17
///     transfer leadership, 1001 logo (USE_GUILD_LOGO). Dead sub-commands reproduce their exact legacy
///     shape: 11 aborts the connection (<c>Quit()</c> is unconditional, dead code follows it); 12/13 are a
///     SILENT no-op (no abort, no response -- the legacy's own <c>Quit()</c> call is commented out); 15/16
///     and anything else fall to the legacy's own <c>default:</c>, which aborts.
/// </summary>
/// <remarks>
///     Legacy architecture note this handler collapses: every real tSort here used to be a BLOCKING RPC to
///     ts25extra (which owned the actual MySQL calls) -- "extra never validates the caller's role, it
///     trusts the zone" (doc 10 §1 intro). Fenrir folds both halves into one handler: the role/state
///     validations that used to live zone-side stay exactly where they were (checked against
///     <see cref="PlayerRuntimeState" /> before any DB call), and the DB calls that used to live in
///     ts25extra are now <see cref="GuildRepository" /> stored-procedure calls, wrapped in that proc's own
///     transaction instead of ts25extra's manual UpdateGuildInfo/UpdateAvatarGuild-with-rollback dance.
///     <para>
///     Cross-zone propagation is DELIBERATELY narrower than a byte-for-byte port: doc 10 quirk 6 already
///     documents that create/exit/disband/upgrade/transfer(17) propagate NOTHING to other zones in the
///     legacy either (only notice/kick/AGM/title/buff/logo push via center) -- and of THOSE, the notice/
///     buff/logo pushes ride the legacy's generic ZC_BROADCAST_INFO_RECV (op 94) channel, which
///     contracts/06_guild_tribe.md's own "Rappel périmètre" explicitly assigns to a SEPARATE chat/broadcast
///     lot, not this one. This handler therefore mirrors <see cref="PlayerRuntimeState" /> ONLY for the
///     character(s) whose own membership actually changed (kick/AGM/title's target, invite-finalize's
///     invitee, transfer's outgoing/incoming master) -- never a guild-wide cosmetic refresh broadcast.
///     </para>
/// </remarks>
public sealed class GuildActionHandler(
    ZoneRegistry zones,
    GuildRepository guilds,
    CharacterRepository characters,
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

        // Same blanket posture as GenericActionHandler: every tSort this handler covers shares the same
        // per-character economy-adjacent state (guild membership, money for create/upgrade) -- see
        // PlayerRuntimeState.EconomyActionLock's own remarks.
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
                // GuildMark -- Quit() is unconditional in the source, everything after it is dead code.
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case 12:
            case 13:
                // Mark effect ON/OFF -- Quit() is COMMENTED OUT in the source; a bare `return;` follows,
                // so the legacy neither disconnects nor responds. Reproduced exactly: do nothing.
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

    /// <summary>tSort 1 -- create a guild (S04_MyWork02.cpp:9985-10038). Level &gt;=30 and Money &gt;=10M, else <c>Quit()</c>; name non-empty and not already in a guild.</summary>
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
            // Legacy ORDER preserved exactly (S04_MyWork02.cpp:10008-10025): the guild is created FIRST, the
            // money debit happens only AFTER that succeeds. CORRECTION (review finding): the legacy is only
            // safe doing this because it pre-verifies `wAvatar.aMoney >= tCreateGuildMoney` (S04_MyWork02.cpp:
            // 9999-10003) and Quit()s BEFORE ever attempting creation if insufficient -- Fenrir has no cached
            // Money field on PlayerRuntimeState to run that same non-mutating pre-check, so a prior pass here
            // skipped it entirely and let a 0-money character keep a free guild whenever the debit below
            // failed. The debit's own failure path now rolls the just-created guild back (see below) instead
            // of merely logging -- this reproduces the legacy's real guarantee ("no guild without sufficient
            // funds") even though the exact mechanism (rollback vs. pre-check) differs.
            guildId = await guilds.CreateAsync(name, characterId, ct);
        }
        catch (Exception ex)
        {
            // Name already taken (THROW 50230) or already in a guild (50231, a benign race against this
            // same lock's own state.GuildId check above).
            logger.LogInformation(ex, "Character {CharacterId} guild create failed for name {GuildName}",
                characterId, name);
            SendResult(session, 1, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        try
        {
            await characters.AdjustMoneyAsync(characterId, -CreateGuildMoneyCost, 0, ct);
        }
        catch (Exception ex)
        {
            // Insufficient funds -- roll back (disband) the just-created guild rather than leave a paid-for
            // feature the character never actually paid for, then Quit() like the legacy's own upfront check.
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

    /// <summary>tSort 2 -- info/roster refresh (S04_MyWork02.cpp:10040-10065). Must already be in a guild.</summary>
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
    ///     tSort 3 -- invite finalize (S04_MyWork02.cpp:10066-10161). Requires role 0/1 (master/sub-master)
    ///     and a pending accepted invite (<see cref="GuildInviteRegistry.TryConsumeAccepted" /> -- legacy
    ///     state 3 on both sides). Member cap = Grade*10 (tResult=2 if reached).
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
            SendResult(session, 3, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        var guild = await guilds.GetByIdAsync(guildId, ct);
        if (guild is null)
        {
            SendResult(session, 3, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        var roster = await guilds.GetRosterAsync(guildId, ct);
        if (roster.Count >= guild.Grade * 10)
        {
            SendResult(session, 3, GuildInfoProjection.Empty(), result: 2);
            return;
        }

        try
        {
            await guilds.AddMemberAsync(guildId, inviteeId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} guild invite-finalize add failed for invitee {InviteeId}",
                characterId, inviteeId);
            SendResult(session, 3, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        SendResult(session, 3, await BuildGuildInfoAsync(guildId, ct));

        if (zones.TryGetPlayerAndZone(inviteeId, out _, out var inviteeZone))
            inviteeZone.PostGuildCommand(new GuildMembershipZoneCommand(inviteeId, guildId, guild.Name,
                GuildRoleCodec.WireRoleToDb(2), ""));
    }

    /// <summary>tSort 4 -- voluntary exit (S04_MyWork02.cpp:10162-10199). The master cannot leave (must transfer/disband instead).</summary>
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

    /// <summary>tSort 5 -- guild notice, master only (S04_MyWork02.cpp:10200-10236, GUILD_NOTICE_V2).</summary>
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

        SendResult(session, 5, await BuildGuildInfoAsync(guildId, ct));
    }

    /// <summary>tSort 6 -- disband, master only, requires exactly 1 remaining member (S04_MyWork02.cpp:10237-10302).</summary>
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
            SendResult(session, 6, GuildInfoProjection.Empty(), result: 2);
            return;
        }

        await guilds.DisbandAsync(guildId, ct);

        // Post-delete, there is nothing left to re-fetch -- Empty(), a reasonable, documented choice (the
        // legacy quirk here is uninitialized stack garbage regardless, see GuildActionResponse's remarks).
        SendResult(session, 6, GuildInfoProjection.Empty());

        // No cross-zone propagation for the OTHER (already-removed, by cascade) members -- matches doc 10
        // quirk 6 exactly ("create/exit/delete/upgrade/transfer ne propagent rien"): their own cached guild
        // fields simply go stale until their next tSort-2 query, same as the legacy.
        await zone.PostGuildCommandAndWaitAsync(new GuildMembershipZoneCommand(characterId, null, "", 0, ""), ct);
    }

    /// <summary>tSort 7 -- grade upgrade, master only (S04_MyWork02.cpp:10303-10418). Member count must already be at cap; level/cost thresholds per grade.</summary>
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
            _ => (-1, -1) // grade >= 5 -- Quit()
        };

        if (requiredLevel < 0 || state.Level < requiredLevel)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var previousGrade = guild.Grade;
        try
        {
            // Legacy ORDER preserved: grade increments FIRST, money debited after (S04_MyWork02.cpp:10396-10414).
            // CORRECTION (review finding): the legacy only reaches this point after pre-verifying
            // `wAvatar.aMoney >= tCost` per-grade (S04_MyWork02.cpp:10339-10382) and Quit()ing BEFORE the
            // upgrade RPC if insufficient -- Fenrir has no cached Money field to run that same non-mutating
            // pre-check, so a failed debit below now rolls the grade increment back instead of merely logging.
            await guilds.SetGradeAsync(guildId, previousGrade + 1, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} guild upgrade failed for guild {GuildId}",
                characterId, guildId);
            SendResult(session, 7, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        try
        {
            await characters.AdjustMoneyAsync(characterId, -cost, 0, ct);
        }
        catch (Exception ex)
        {
            // Insufficient funds -- roll the grade increment back rather than leave a paid-for upgrade the
            // character never actually paid for, then Quit() like the legacy's own upfront check.
            logger.LogWarning(ex,
                "Character {CharacterId} guild upgrade money debit failed -- rolling back guild {GuildId} to grade {PreviousGrade}",
                characterId, guildId, previousGrade);
            await guilds.SetGradeAsync(guildId, previousGrade, ct);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        SendResult(session, 7, await BuildGuildInfoAsync(guildId, ct));
    }

    /// <summary>tSort 8 -- kick, master only (S04_MyWork02.cpp:10419-10448). Target resolved by name, independent of online state; the master cannot be kicked.</summary>
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
            // Not a member, or IS the master (doc 10 §1 tSort 8: "vérifie que ce n'est pas gMaster01") --
            // clean fail, not a Quit (the legacy's own extra-side validation has no Quit path here either).
            SendResult(session, 8, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        await guilds.RemoveMemberAsync(guildId, target.CharacterId, ct);

        SendResult(session, 8, await BuildGuildInfoAsync(guildId, ct));

        if (zones.TryGetPlayerAndZone(target.CharacterId, out _, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, null, "", 0, ""));
    }

    /// <summary>
    ///     tSort 9 -- AGM promote (1)/demote (2), master only (S04_MyWork02.cpp:10449-10479). Promote:
    ///     refused if the guild already has <see cref="MaxSubMasters" /> sub-masters, or the target isn't
    ///     currently a plain member. Demote: target must currently be a sub-master.
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
            SendResult(session, 9, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        const byte dbMember = 0;
        const byte dbSubMaster = 1;

        if (payload.GuildRole == 1)
        {
            var subMasterCount = roster.Count(r => r.Role == dbSubMaster);
            if (subMasterCount >= MaxSubMasters || target.Role != dbMember)
            {
                SendResult(session, 9, GuildInfoProjection.Empty(), result: 1);
                return;
            }
        }
        else
        {
            if (target.Role != dbSubMaster)
            {
                SendResult(session, 9, GuildInfoProjection.Empty(), result: 1);
                return;
            }
        }

        var newRole = payload.GuildRole == 1 ? dbSubMaster : dbMember;
        await guilds.SetRoleAsync(guildId, target.CharacterId, newRole, ct);
        await guilds.SetCallNameAsync(guildId, target.CharacterId, "", ct);

        SendResult(session, 9, await BuildGuildInfoAsync(guildId, ct));

        if (zones.TryGetPlayerAndZone(target.CharacterId, out var targetState, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, guildId,
                targetState.GuildName, newRole, ""));
    }

    /// <summary>tSort 10 -- member title/CallName, master only (S04_MyWork02.cpp:10480-10511). Target must be a member, not the master.</summary>
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
            SendResult(session, 10, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        var callName = payload.CallName.Trim();
        await guilds.SetCallNameAsync(guildId, target.CharacterId, callName, ct);

        SendResult(session, 10, await BuildGuildInfoAsync(guildId, ct));

        if (zones.TryGetPlayerAndZone(target.CharacterId, out var targetState, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, guildId,
                targetState.GuildName, targetState.GuildRoleDb, callName));
    }

    /// <summary>
    ///     tSort 14 -- buff type choice, master/sub-master only (S04_MyWork02.cpp:10551-10611,
    ///     USE_GUILD_BUFF). Member (wire role 2) gets a CLEAN tResult=4, not a Quit. Requires at least 1
    ///     minute of buff-time reserve (only ever recharged by tSort 15's guild scrolls -- doc 10 quirk 12).
    /// </summary>
    /// <remarks>
    ///     OPEN ISSUE: the guild buff's actual gameplay STAT effect is not documented anywhere in report 11's
    ///     own exhaustive StatCalculator catalog (gBuffType/gBuffState never appear there) -- only this
    ///     state machine (choose a type, track remaining reserve minutes) is implemented; no stat bonus is
    ///     invented for it. The legacy's own cross-zone live push (center opcodes 112/113, doc 10 §1) rides
    ///     ZC_BROADCAST_INFO_RECV op 94, which contracts/06_guild_tribe.md assigns to the separate chat/
    ///     broadcast lot -- not reproduced here (see class remarks).
    /// </remarks>
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
            SendResult(session, 14, GuildInfoProjection.Empty(), result: 4);
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
            SendResult(session, 14, GuildInfoProjection.Empty(), result: 2);
            return;
        }

        try
        {
            await guilds.SetBuffAsync(guildId, payload.GuildBuffType, 1, guild.BuffTime, guild.BuffTimeForDiff, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Guild {GuildId} buff type update failed", guildId);
            SendResult(session, 14, GuildInfoProjection.Empty(), result: 5);
            return;
        }

        SendResult(session, 14, await BuildGuildInfoAsync(guildId, ct));
    }

    /// <summary>
    ///     tSort 17 -- transfer leadership, master only (S04_MyWork02.cpp:10613-10665). Target resolved
    ///     WITHIN THE ACTOR'S OWN ZONE ONLY (<c>SearchAvatar</c> scope, doc 10 quirk 13). Success responds
    ///     tResult=2 (NOT 0 -- a verified legacy quirk the EU33 client expects, doc 10 quirk 1).
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
            SendResult(session, 17, GuildInfoProjection.Empty(), result: 1);
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
            SendResult(session, 17, GuildInfoProjection.Empty(), result: 1);
            return;
        }

        // Quirk (doc 10 quirk 1): success responds tResult=2, not 0 -- the EU33 client expects this exact value.
        SendResult(session, 17, await BuildGuildInfoAsync(guildId, ct), result: 2);

        await zone.PostGuildCommandAndWaitAsync(
            new GuildMembershipZoneCommand(characterId, guildId, state.GuildName, GuildRoleCodec.WireRoleToDb(2), ""),
            ct);
        zone.PostGuildCommand(new GuildMembershipZoneCommand(newMaster.CharacterId, guildId, state.GuildName,
            GuildRoleCodec.WireRoleToDb(0), ""));
    }

    /// <summary>
    ///     tSort 1001 -- logo, master only (S04_MyWork02.cpp:10666-10696, USE_GUILD_LOGO). ALWAYS responds
    ///     with an empty GUILD_INFO, even on success -- doc 10 quirk 4 (the legacy's own uninitialized-stack
    ///     response for this one specific success case).
    /// </summary>
    /// <remarks>
    ///     DELIBERATE DEVIATION: the legacy checks ONLY <c>wAvatar.aGuildRole != 0</c>, never
    ///     <c>IsEmptyString(wAvatar.aGuildName)</c> -- doc 10 quirk notes this is an accidental pass-through
    ///     for a GUILDLESS character (whose <c>aGuildRole</c> also happens to default to 0, the same raw
    ///     value as "master"), landing on a harmless 0-row UPDATE. Fenrir's <see cref="PlayerRuntimeState.GuildRoleDb" />
    ///     defaults to 0 = DB "member" (wire role 2), NOT "master" (wire role 0) via <see cref="GuildRoleCodec" />,
    ///     so a guildless character is correctly rejected here instead of accidentally passing -- a safe,
    ///     documented, zero-real-effect divergence from a genuine legacy accident, not a designed mechanic.
    /// </remarks>
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
