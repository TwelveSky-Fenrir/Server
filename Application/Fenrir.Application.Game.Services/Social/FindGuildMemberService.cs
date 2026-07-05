using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

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
