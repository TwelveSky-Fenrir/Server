using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>
///     Outcome of CZ_GUILD_FIND_SEND, as branched on by <see cref="FindGuildMemberHandler" />.
///     <see cref="HasGuild" /> false means the caller isn't in a guild: legacy's bare return, not a response.
/// </summary>
public readonly record struct FindGuildMemberResult(bool HasGuild, int ZoneNumber);

/// <summary>Business logic behind CZ_GUILD_FIND_SEND.</summary>
public interface IFindGuildMemberService
{
    FindGuildMemberResult FindZone(PlayerRuntimeState asker, string avatarName);
}

/// <inheritdoc cref="IFindGuildMemberService" />
public sealed class FindGuildMemberService(ZoneRegistry zones) : IFindGuildMemberService
{
    public FindGuildMemberResult FindZone(PlayerRuntimeState asker, string avatarName)
    {
        if (asker.GuildId is null)
            return new FindGuildMemberResult(false, -1);

        var zoneNumber = zones.TryGetPlayerByName(avatarName, out var found) ? found.MapId : -1;
        return new FindGuildMemberResult(true, zoneNumber);
    }
}
