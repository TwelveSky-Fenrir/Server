using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IFindGuildMemberService" />
public sealed class FindGuildMemberService(ZoneRegistry zones, ICharacterShardLocationRepository characterShardLocations)
    : IFindGuildMemberService
{
    public async ValueTask<FindGuildMemberResult> FindZoneAsync(PlayerRuntimeState asker, string avatarName,
        CancellationToken cancellationToken)
    {
        if (asker.GuildId is null)
            return new FindGuildMemberResult(false, -1);

        if (zones.TryGetPlayerByName(avatarName, out var found))
            return new FindGuildMemberResult(true, found.MapId);

        // Same-shard miss -- fall back to the cross-shard directory before reporting "not found". A
        // deliberate, low-frequency player action (a guild-find ping), not a per-tick path.
        var remote = await characterShardLocations.FindByNameAsync(avatarName, cancellationToken)
            .ConfigureAwait(false);

        return new FindGuildMemberResult(true, remote?.MapId ?? -1);
    }
}
