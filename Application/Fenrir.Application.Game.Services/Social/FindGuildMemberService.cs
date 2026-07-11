using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class FindGuildMemberService(
    ZoneRegistry zones,
    ICharacterShardLocationRepository characterShardLocations,
    ILogger<FindGuildMemberService> logger)
    : IFindGuildMemberService
{
    public async ValueTask<FindGuildMemberResult> FindZoneAsync(PlayerRuntimeState asker, string avatarName,
        CancellationToken cancellationToken)
    {
        if (asker.GuildId is null)
        {
            logger.LogDebug("Character {CharacterId} guild-find rejected: caller has no guild", asker.CharacterId);
            return new FindGuildMemberResult(false, -1);
        }

        if (zones.TryGetPlayerByName(avatarName, out var found))
        {
            logger.LogDebug(
                "Character {CharacterId} located guild member {AvatarName} on this shard (map {MapId})",
                asker.CharacterId, avatarName, found.MapId);
            return new FindGuildMemberResult(true, found.MapId);
        }

        var remote = await characterShardLocations.FindByNameAsync(avatarName, cancellationToken)
            .ConfigureAwait(false);

        logger.LogDebug(
            "Character {CharacterId} guild-find for {AvatarName} resolved via cross-shard directory: found={Found}",
            asker.CharacterId, avatarName, remote is not null);

        return new FindGuildMemberResult(true, remote?.MapId ?? -1);
    }
}
