using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum MentorEndResultKind
{
    NotBonded,
    Ended
}

public readonly record struct MentorEndResult(MentorEndResultKind Kind);

public interface IMentorEndService
{
    public ValueTask<MentorEndResult> EndAsync(PlayerRuntimeState state, CancellationToken cancellationToken);
}
