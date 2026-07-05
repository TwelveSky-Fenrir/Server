using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Outcome of CZ_GUILD_ASK_SEND's pre-checks, as branched on by <see cref="GuildInviteHandler" />.</summary>
public enum GuildInviteAskResultKind
{
    NotAuthorized, // Abort: asker isn't guild master/sub-master
    TargetNotFound,
    TargetAlreadyGuilded, // Abort
    TribeMismatch, // Abort
    AskerBusy,
    TargetBusy,
    Sent
}

/// <summary>Business logic behind the CZ_GUILD_ASK/ANSWER/CANCEL_SEND opcode family.</summary>
public interface IGuildInviteService
{
    GuildInviteAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName);

    void Answer(int targetId, int answerCode);

    void Cancel(int askerId);
}

/// <inheritdoc cref="IGuildInviteService" />
public sealed class GuildInviteService(ZoneRegistry zones, GuildInviteRegistry invites) : IGuildInviteService
{
    public GuildInviteAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (asker.GuildId is null || !GuildRoleCodec.IsMasterOrSubMaster(asker.GuildRoleDb))
            return GuildInviteAskResultKind.NotAuthorized;

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
            return GuildInviteAskResultKind.TargetNotFound;

        if (target.GuildId is not null)
            return GuildInviteAskResultKind.TargetAlreadyGuilded;

        if (asker.Tribe != target.Tribe)
            return GuildInviteAskResultKind.TribeMismatch;

        switch (invites.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case GuildInviteAskOutcome.AskerBusy:
                return GuildInviteAskResultKind.AskerBusy;
            case GuildInviteAskOutcome.TargetBusy:
                return GuildInviteAskResultKind.TargetBusy;
            case GuildInviteAskOutcome.Sent:
                target.Session.Send(new GuildInviteResponse { AvatarName = asker.Name });
                return GuildInviteAskResultKind.Sent;
            default:
                return GuildInviteAskResultKind.AskerBusy;
        }
    }

    public void Answer(int targetId, int answerCode)
    {
        if (!invites.TryAnswer(targetId, answerCode == 0, out var askerId))
            return;

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new GuildInviteAnswerResponse { Answer = answerCode });
    }

    public void Cancel(int askerId)
    {
        if (!invites.TryCancel(askerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new GuildInviteCancelResponse());
    }

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
