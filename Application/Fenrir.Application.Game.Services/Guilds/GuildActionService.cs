using Fenrir.Application.Game.Abstractions.Guilds;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Guilds;

/// <inheritdoc cref="IGuildActionService" />
public sealed class GuildActionService(
    ZoneRegistry zones,
    IGuildRepository guilds,
    ICharacterRepository characters,
    GuildInviteRegistry invites,
    ILogger<GuildActionService> logger) : IGuildActionService
{
    private const int CreateGuildMoneyCost = 10_000_000;
    internal const int MaxSubMasters = 2;

    public async ValueTask<GuildActionResult> CreateGuildAsync(GuildActionRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not null || !GuildWorkCreatePayload.TryRead(packet.Data, out var payload))
            return GuildActionResult.Aborted;

        var name = payload.GuildName.Trim();
        if (name.Length == 0 || state.Level < 30)
            return GuildActionResult.Aborted;

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
            return GuildActionResult.Success(1, GuildInfoProjection.Empty(), 1);
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
            return GuildActionResult.Aborted;
        }

        var info = await BuildGuildInfoAsync(guildId, ct);

        await zone.PostGuildCommandAndWaitAsync(
            new GuildMembershipZoneCommand(characterId, guildId, name, GuildRoleCodec.WireRoleToDb(0), ""), ct);

        return GuildActionResult.Success(1, info);
    }

    public async ValueTask<GuildActionResult> GetGuildInfoAsync(PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId)
            return GuildActionResult.Aborted;

        return GuildActionResult.Success(2, await BuildGuildInfoAsync(guildId, ct));
    }

    public async ValueTask<GuildActionResult> FinalizeInviteAsync(PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMasterOrSubMaster(state.GuildRoleDb))
            return GuildActionResult.Aborted;

        if (!invites.TryConsumeAccepted(characterId, out var inviteeId))
            return GuildActionResult.Success(3, GuildInfoProjection.Empty(), 1);

        var guild = await guilds.GetByIdAsync(guildId, ct);
        if (guild is null)
            return GuildActionResult.Success(3, GuildInfoProjection.Empty(), 1);

        var roster = await guilds.GetRosterAsync(guildId, ct);
        if (roster.Count >= guild.Grade * 10)
            return GuildActionResult.Success(3, GuildInfoProjection.Empty(), 2);

        try
        {
            await guilds.AddMemberAsync(guildId, inviteeId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} guild invite-finalize add failed for invitee {InviteeId}",
                characterId, inviteeId);
            return GuildActionResult.Success(3, GuildInfoProjection.Empty(), 1);
        }

        var info = await BuildGuildInfoAsync(guildId, ct);

        if (zones.TryGetPlayerAndZone(inviteeId, out _, out var inviteeZone))
            inviteeZone.PostGuildCommand(new GuildMembershipZoneCommand(inviteeId, guildId, guild.Name,
                GuildRoleCodec.WireRoleToDb(2), ""));

        return GuildActionResult.Success(3, info);
    }

    public async ValueTask<GuildActionResult> ExitGuildAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || GuildRoleCodec.IsMaster(state.GuildRoleDb))
            return GuildActionResult.Aborted;

        await guilds.RemoveMemberAsync(guildId, characterId, ct);

        var info = await BuildGuildInfoAsync(guildId, ct);

        await zone.PostGuildCommandAndWaitAsync(new GuildMembershipZoneCommand(characterId, null, "", 0, ""), ct);

        return GuildActionResult.Success(4, info);
    }

    public async ValueTask<GuildActionResult> UpdateGuildNoticeAsync(GuildActionRequest packet,
        PlayerRuntimeState state, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkNoticePayload.TryRead(packet.Data, out var payload))
            return GuildActionResult.Aborted;

        for (byte i = 0; i < payload.Notices.Length; i++)
            await guilds.SetNoticeAsync(guildId, i, payload.Notices[i].Trim(), ct);

        var info = await BuildGuildInfoAsync(guildId, ct);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 5, info, state.CharacterId);

        return GuildActionResult.Success(5, info);
    }

    public async ValueTask<GuildActionResult> DisbandGuildAsync(Zone zone, PlayerRuntimeState state,
        int characterId, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb))
            return GuildActionResult.Aborted;

        var roster = await guilds.GetRosterAsync(guildId, ct);
        if (roster.Count != 1)
            return GuildActionResult.Success(6, GuildInfoProjection.Empty(), 2);

        await guilds.DisbandAsync(guildId, ct);

        await zone.PostGuildCommandAndWaitAsync(new GuildMembershipZoneCommand(characterId, null, "", 0, ""), ct);

        return GuildActionResult.Success(6, GuildInfoProjection.Empty());
    }

    public async ValueTask<GuildActionResult> UpgradeGuildAsync(PlayerRuntimeState state, int characterId,
        CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb))
            return GuildActionResult.Aborted;

        var guild = await guilds.GetByIdAsync(guildId, ct);
        if (guild is null)
            return GuildActionResult.Aborted;

        var roster = await guilds.GetRosterAsync(guildId, ct);
        if (roster.Count < guild.Grade * 10)
            return GuildActionResult.Aborted;

        var (requiredLevel, cost) = guild.Grade switch
        {
            1 => (50, 20_000_000),
            2 => (70, 30_000_000),
            3 => (90, 40_000_000),
            4 => (113, 50_000_000),
            _ => (-1, -1)
        };

        if (requiredLevel < 0 || state.Level < requiredLevel)
            return GuildActionResult.Aborted;

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
            return GuildActionResult.Success(7, GuildInfoProjection.Empty(), 1);
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
            return GuildActionResult.Aborted;
        }

        return GuildActionResult.Success(7, await BuildGuildInfoAsync(guildId, ct));
    }

    public async ValueTask<GuildActionResult> KickMemberAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkKickPayload.TryRead(packet.Data, out var payload))
            return GuildActionResult.Aborted;

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
            return GuildActionResult.Aborted;

        var roster = await guilds.GetRosterAsync(guildId, ct);
        var target = FindMember(roster, targetName);
        if (target is null || GuildRoleCodec.IsMaster(target.Role))
            return GuildActionResult.Success(8, GuildInfoProjection.Empty(), 1);

        await guilds.RemoveMemberAsync(guildId, target.CharacterId, ct);

        var info = await BuildGuildInfoAsync(guildId, ct);

        if (zones.TryGetPlayerAndZone(target.CharacterId, out _, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, null, "", 0, ""));

        return GuildActionResult.Success(8, info);
    }

    public async ValueTask<GuildActionResult> SetAgmRoleAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkAgmPayload.TryRead(packet.Data, out var payload) || payload.GuildRole is < 1 or > 2)
            return GuildActionResult.Aborted;

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
            return GuildActionResult.Aborted;

        var roster = await guilds.GetRosterAsync(guildId, ct);
        var target = FindMember(roster, targetName);
        if (target is null)
            return GuildActionResult.Success(9, GuildInfoProjection.Empty(), 1);

        const byte dbMember = 0;
        const byte dbSubMaster = 1;

        if (payload.GuildRole == 1)
        {
            var subMasterCount = roster.Count(r => r.Role == dbSubMaster);
            if (subMasterCount >= MaxSubMasters || target.Role != dbMember)
                return GuildActionResult.Success(9, GuildInfoProjection.Empty(), 1);
        }
        else
        {
            if (target.Role != dbSubMaster)
                return GuildActionResult.Success(9, GuildInfoProjection.Empty(), 1);
        }

        var newRole = payload.GuildRole == 1 ? dbSubMaster : dbMember;
        await guilds.SetRoleAsync(guildId, target.CharacterId, newRole, ct);
        await guilds.SetCallNameAsync(guildId, target.CharacterId, "", ct);

        var info = await BuildGuildInfoAsync(guildId, ct);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 9, info, state.CharacterId);

        if (zones.TryGetPlayerAndZone(target.CharacterId, out var targetState, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, guildId,
                targetState.GuildName, newRole, ""));

        return GuildActionResult.Success(9, info);
    }

    public async ValueTask<GuildActionResult> SetMemberTitleAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkTitlePayload.TryRead(packet.Data, out var payload))
            return GuildActionResult.Aborted;

        var targetName = payload.AvatarName.Trim();
        if (targetName.Length == 0)
            return GuildActionResult.Aborted;

        var roster = await guilds.GetRosterAsync(guildId, ct);
        var target = FindMember(roster, targetName);
        if (target is null || GuildRoleCodec.IsMaster(target.Role))
            return GuildActionResult.Success(10, GuildInfoProjection.Empty(), 1);

        var callName = payload.CallName.Trim();
        await guilds.SetCallNameAsync(guildId, target.CharacterId, callName, ct);

        var info = await BuildGuildInfoAsync(guildId, ct);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 10, info, state.CharacterId);

        if (zones.TryGetPlayerAndZone(target.CharacterId, out var targetState, out var targetZone))
            targetZone.PostGuildCommand(new GuildMembershipZoneCommand(target.CharacterId, guildId,
                targetState.GuildName, targetState.GuildRoleDb, callName));

        return GuildActionResult.Success(10, info);
    }

    public async ValueTask<GuildActionResult> SetGuildBuffAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct)
    {
        if (state.GuildId is not { } guildId)
            return GuildActionResult.Aborted;

        if (!GuildRoleCodec.IsMasterOrSubMaster(state.GuildRoleDb))
            return GuildActionResult.Success(14, GuildInfoProjection.Empty(), 4);

        if (!GuildWorkBuffPayload.TryRead(packet.Data, out var payload) || payload.GuildBuffType is < 0 or > 4)
            return GuildActionResult.Aborted;

        var guild = await guilds.GetByIdAsync(guildId, ct);
        if (guild is null)
            return GuildActionResult.Aborted;

        if (guild.BuffTime < 1)
            return GuildActionResult.Success(14, GuildInfoProjection.Empty(), 2);

        try
        {
            await guilds.SetBuffAsync(guildId, payload.GuildBuffType, 1, guild.BuffTime, DateTime.UtcNow.Ticks, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Guild {GuildId} buff type update failed", guildId);
            return GuildActionResult.Success(14, GuildInfoProjection.Empty(), 5);
        }

        var info = await BuildGuildInfoAsync(guildId, ct);
        GuildInfoBroadcaster.BroadcastGuildInfo(zones, guildId, 14, info, state.CharacterId);

        return GuildActionResult.Success(14, info);
    }

    public async ValueTask<GuildActionResult> TransferLeadershipAsync(GuildActionRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        if (state.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(state.GuildRoleDb) ||
            !GuildWorkTransferPayload.TryRead(packet.Data, out var payload))
            return GuildActionResult.Aborted;

        var newMasterName = payload.NewMasterName.Trim();
        if (newMasterName.Length == 0)
            return GuildActionResult.Aborted;

        PlayerRuntimeState? newMaster = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, newMasterName, StringComparison.OrdinalIgnoreCase))
            {
                newMaster = candidate;
                break;
            }

        if (newMaster is null)
            return GuildActionResult.Success(17, GuildInfoProjection.Empty(), 1);

        try
        {
            await guilds.SetMasterAsync(guildId, newMaster.CharacterId, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Character {CharacterId} guild leadership transfer to {NewMasterId} failed",
                characterId, newMaster.CharacterId);
            return GuildActionResult.Success(17, GuildInfoProjection.Empty(), 1);
        }

        var info = await BuildGuildInfoAsync(guildId, ct);

        await zone.PostGuildCommandAndWaitAsync(
            new GuildMembershipZoneCommand(characterId, guildId, state.GuildName, GuildRoleCodec.WireRoleToDb(2), ""),
            ct);
        zone.PostGuildCommand(new GuildMembershipZoneCommand(newMaster.CharacterId, guildId, state.GuildName,
            GuildRoleCodec.WireRoleToDb(0), ""));

        return GuildActionResult.Success(17, info, 2);
    }

    public async ValueTask<GuildActionResult> SetGuildLogoAsync(GuildActionRequest packet, PlayerRuntimeState state,
        CancellationToken ct)
    {
        if (!GuildRoleCodec.IsMaster(state.GuildRoleDb) || state.GuildId is not { } guildId ||
            !GuildWorkLogoPayload.TryRead(packet.Data, out var payload))
            return GuildActionResult.Aborted;

        try
        {
            await guilds.SetLogoAsync(guildId, payload.Value, ct);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Guild {GuildId} logo update failed", guildId);
        }

        return GuildActionResult.Success(1001, GuildInfoProjection.Empty());
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
}
