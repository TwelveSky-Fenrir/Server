using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct FindGuildMemberResult(bool HasGuild, int ZoneNumber);

public interface IFindGuildMemberService
{

        public ValueTask<FindGuildMemberResult> FindZoneAsync(PlayerRuntimeState asker, string avatarName,
        CancellationToken cancellationToken);
}
