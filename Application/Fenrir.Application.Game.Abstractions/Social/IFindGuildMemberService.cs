using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>
///     Outcome of CZ_GUILD_FIND_SEND, as branched on by <see cref="FindGuildMemberHandler" />.
///     <see cref="HasGuild" /> false means the caller isn't in a guild: legacy's bare return, not a response.
/// </summary>
public readonly record struct FindGuildMemberResult(bool HasGuild, int ZoneNumber);

/// <summary>Business logic behind CZ_GUILD_FIND_SEND.</summary>
public interface IFindGuildMemberService
{
    /// <summary>
    ///     Same-shard lookup first (<see cref="ZoneRegistry.TryGetPlayerByName" />), falling back to the
    ///     cross-shard character-location directory on a miss -- this opcode only ever needs a
    ///     <c>ZoneNumber</c> reply, so the directory's <c>MapId</c> answers it directly with no live delivery
    ///     required (unlike whisper).
    /// </summary>
    public ValueTask<FindGuildMemberResult> FindZoneAsync(PlayerRuntimeState asker, string avatarName,
        CancellationToken cancellationToken);
}
